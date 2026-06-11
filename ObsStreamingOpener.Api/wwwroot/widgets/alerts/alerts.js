import { getJson, query } from "../shared.js";

const root = document.getElementById("root");
const card = document.getElementById("alert");
const eyebrow = document.getElementById("eyebrow");
const title = document.getElementById("title");
const message = document.getElementById("message");
const meta = document.getElementById("meta");
const media = document.getElementById("media");

const channelId = query("channelId", "");
const streamSessionId = query("streamSessionId", "");
const preview = query("preview", "false") === "true";
const interval = Math.max(Number(query("interval", "1000")), 500);
let settings = {
  theme: "default",
  queueOrdering: "shortest-first",
  minDurationMs: 1000,
  maxDurationMs: 60000,
  defaultSoundUrl: null,
  defaultMediaUrl: null,
  animationPreset: "sparkles",
  volume: 0.8,
  autoAck: true
};

if (preview) {
  root.classList.add("preview");
}

const queue = [];
const queuedIds = new Set();
const playedIds = new Set();
let playing = false;

function cssClass(value) {
  return String(value ?? "default").replace(/[^a-z0-9_-]/gi, "").toLowerCase() || "default";
}

function alertDurationMs(alert) {
  const from = Date.parse(alert.displayFromUtc);
  const until = Date.parse(alert.displayUntilUtc);
  if (!Number.isFinite(from) || !Number.isFinite(until)) {
    return 6000;
  }

  return Math.min(Math.max(until - from, settings.minDurationMs), settings.maxDurationMs);
}

function render(alert) {
  if (!alert) {
    card.className = "alert-card hidden";
    media.className = "media hidden";
    media.removeAttribute("src");
    return;
  }

  root.dataset.theme = cssClass(settings.theme);
  root.dataset.animation = cssClass(settings.animationPreset);
  card.className = `alert-card ${cssClass(alert.visualStyle)}`;
  eyebrow.textContent = alert.isSystemAlert ? "System alert" : alert.alertType;
  title.textContent = alert.title;
  message.textContent = alert.message ?? "";
  meta.textContent = alert.amount ? `${alert.amount} ${alert.currency ?? ""}`.trim() : (alert.actorName ?? "");

  const mediaUrl = alert.mediaUrl || settings.defaultMediaUrl;
  if (mediaUrl) {
    media.src = mediaUrl;
    media.className = "media";
  } else {
    media.className = "media hidden";
    media.removeAttribute("src");
  }
}

function enqueue(alerts) {
  for (const alert of alerts ?? []) {
    if (!alert?.id || playedIds.has(alert.id) || queuedIds.has(alert.id)) {
      continue;
    }

    queue.push(alert);
    queuedIds.add(alert.id);
  }

  queue.sort((a, b) => {
    const oldestFirst = Date.parse(a.displayFromUtc) - Date.parse(b.displayFromUtc);
    if (settings.queueOrdering === "oldest-first") {
      return oldestFirst;
    }

    return alertDurationMs(a) - alertDurationMs(b) || oldestFirst;
  });
  playNext();
}

async function acknowledge(alert) {
  if (preview || !settings.autoAck || !channelId || !alert?.id) {
    return;
  }

  await fetch(`/api/channels/${channelId}/alerts/${alert.id}/ack`, { method: "POST" });
}

function playSound(alert) {
  const soundUrl = alert.soundUrl || settings.defaultSoundUrl;
  if (!soundUrl) {
    return;
  }

  const audio = new Audio(soundUrl);
  audio.volume = Math.min(Math.max(Number(settings.volume ?? 0.8), 0), 1);
  audio.play().catch(console.error);
}

function playNext() {
  if (playing) {
    return;
  }

  const alert = queue.shift();
  if (!alert) {
    render(null);
    return;
  }

  queuedIds.delete(alert.id);
  playedIds.add(alert.id);
  playing = true;
  render(alert);
  playSound(alert);
  window.setTimeout(() => {
    acknowledge(alert).catch(console.error);
    render(null);
    playing = false;
    playNext();
  }, alertDurationMs(alert));
}

async function refresh() {
  const params = new URLSearchParams();
  if (channelId) {
    params.set("channelId", channelId);
  }

  if (streamSessionId) {
    params.set("streamSessionId", streamSessionId);
  }

  const data = await getJson(`/api/widgets/alerts/data?${params}`);
  settings = { ...settings, ...(data.settings ?? {}) };
  enqueue(data.alerts ?? []);
}

function connectSignalR() {
  if (!window.signalR || !channelId) {
    return;
  }

  const connection = new window.signalR.HubConnectionBuilder()
    .withUrl("/hubs/alerts")
    .withAutomaticReconnect()
    .build();

  connection.on("alertCreated", (alert) => enqueue([alert]));
  connection.start()
    .then(() => connection.invoke("Subscribe", channelId, streamSessionId || null))
    .catch(console.error);
}

refresh().catch(console.error);
setInterval(() => refresh().catch(console.error), interval);
connectSignalR();

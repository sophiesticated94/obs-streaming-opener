import { getJson, query } from "../shared.js";

const root = document.getElementById("root");
const card = document.getElementById("alert");
const eyebrow = document.getElementById("eyebrow");
const title = document.getElementById("title");
const message = document.getElementById("message");
const meta = document.getElementById("meta");

const channelId = query("channelId", "");
const streamSessionId = query("streamSessionId", "");
const preview = query("preview", "false") === "true";
const interval = Math.max(Number(query("interval", "1000")), 500);
const minDurationMs = 1000;
const maxDurationMs = 60000;

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

  return Math.min(Math.max(until - from, minDurationMs), maxDurationMs);
}

function render(alert) {
  if (!alert) {
    card.className = "alert-card hidden";
    return;
  }

  card.className = `alert-card ${cssClass(alert.visualStyle)}`;
  eyebrow.textContent = alert.isSystemAlert ? "System alert" : alert.alertType;
  title.textContent = alert.title;
  message.textContent = alert.message ?? "";
  meta.textContent = alert.amount ? `${alert.amount} ${alert.currency ?? ""}`.trim() : (alert.actorName ?? "");
}

function enqueue(alerts) {
  for (const alert of alerts ?? []) {
    if (!alert?.id || playedIds.has(alert.id) || queuedIds.has(alert.id)) {
      continue;
    }

    queue.push(alert);
    queuedIds.add(alert.id);
  }

  queue.sort((a, b) => alertDurationMs(a) - alertDurationMs(b)
    || Date.parse(a.displayFromUtc) - Date.parse(b.displayFromUtc));
  playNext();
}

async function acknowledge(alert) {
  if (preview || !channelId || !alert?.id) {
    return;
  }

  await fetch(`/api/channels/${channelId}/alerts/${alert.id}/ack`, { method: "POST" });
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

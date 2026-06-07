import { getJson, query } from "../shared.js";

const interval = Math.max(Number(query("interval", "2000")), 1000);
const limit = Math.min(Math.max(Number(query("limit", "10")), 1), 50);
const channelId = query("channelId", "");
const source = query("source", "");
const status = document.getElementById("status");
const messages = document.getElementById("messages");

function initials(name) {
  return (name ?? "?")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join("") || "?";
}

function avatar(message) {
  if (message.authorProfileImageUrl) {
    return `<span class="avatar"><img src="${escapeHtml(message.authorProfileImageUrl)}" alt=""></span>`;
  }

  return `<span class="avatar">${escapeHtml(initials(message.authorDisplayName))}</span>`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function render(data) {
  const items = data.messages ?? [];
  if (!items.length) {
    messages.innerHTML = `<div class="empty">Waiting for comments</div>`;
    return;
  }

  messages.innerHTML = items.map((message) => `
    <article class="message">
      ${avatar(message)}
      <div>
        <p class="author">${escapeHtml(message.authorDisplayName ?? "Viewer")}</p>
        <p class="text">${escapeHtml(message.messageText ?? "")}</p>
      </div>
      <time class="meta">${new Date(message.publishedAt).toLocaleTimeString()}</time>
    </article>
  `).join("");
}

async function refresh() {
  const params = new URLSearchParams({ limit: String(limit) });
  if (channelId) {
    params.set("channelId", channelId);
  }

  if (source) {
    params.set("source", source);
  }

  const data = await getJson(`/api/widgets/comment-explorer/data?${params}`);
  render(data);
  status.textContent = new Date(data.refreshedAt).toLocaleTimeString();
}

refresh().catch((error) => {
  status.textContent = "offline";
  console.error(error);
});

setInterval(() => refresh().catch((error) => {
  status.textContent = "offline";
  console.error(error);
}), interval);

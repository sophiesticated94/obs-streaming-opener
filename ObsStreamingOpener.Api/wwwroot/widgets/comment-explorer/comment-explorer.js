import { getJson, query } from "../shared.js";
import { connectWidgetHub } from "../realtime.js";

const limit = Math.min(Math.max(Number(query("limit", "10")), 1), 50);
const channelId = query("channelId", "");
const source = query("source", "");
const status = document.getElementById("status");
const messages = document.getElementById("messages");
const items = [];

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

function render() {
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

function addMessage(message) {
  if (!message?.id || items.some((item) => item.id === message.id)) {
    return;
  }

  if (source && message.source !== source) {
    return;
  }

  items.unshift(message);
  items.splice(limit);
  render();
  status.textContent = "live";
}

async function init() {
  const params = new URLSearchParams({ limit: String(limit) });
  if (channelId) {
    params.set("channelId", channelId);
  }

  if (source) {
    params.set("source", source);
  }

  const data = await getJson(`/api/widgets/comment-explorer/data?${params}`);
  items.splice(0, items.length, ...(data.messages ?? []));
  render();
  status.textContent = new Date(data.refreshedAt).toLocaleTimeString();

  await connectWidgetHub({
    hubUrl: "/hubs/activity",
    channelId,
    handlers: { messageCreated: addMessage }
  });
}

init().catch((error) => {
  status.textContent = "offline";
  console.error(error);
});

const signalRScriptUrl = "/lib/microsoft/signalr/dist/browser/signalr.min.js";

let signalRLoadPromise;

export async function connectWidgetHub({ hubUrl, channelId, streamSessionId = null, handlers = {} }) {
  await ensureSignalR();
  const connection = new window.signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(window.signalR.LogLevel.Warning)
    .build();

  for (const [eventName, handler] of Object.entries(handlers)) {
    connection.on(eventName, handler);
  }

  await connection.start();
  if (channelId) {
    await connection.invoke("Subscribe", channelId, streamSessionId || null);
  }

  return connection;
}

async function ensureSignalR() {
  if (window.signalR) {
    return;
  }

  signalRLoadPromise ??= new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = signalRScriptUrl;
    script.async = true;
    script.onload = resolve;
    script.onerror = () => reject(new Error(`Could not load ${signalRScriptUrl}`));
    document.head.appendChild(script);
  });

  await signalRLoadPromise;
}

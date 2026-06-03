export function query(name, fallback) {
  return new URLSearchParams(window.location.search).get(name) ?? fallback;
}

export function applyTheme() {
  const theme = query("theme", "default");
  document.documentElement.dataset.theme = theme;
}

export async function getJson(url) {
  const response = await fetch(url, { cache: "no-store" });
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }

  return response.json();
}

export function formatNumber(value) {
  return new Intl.NumberFormat().format(Number(value ?? 0));
}

export function formatMoney(value, currency) {
  if (!currency) {
    return formatNumber(value);
  }

  return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(Number(value ?? 0));
}

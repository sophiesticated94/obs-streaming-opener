import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const read = (path) => readFileSync(join(root, path), 'utf8');

const routes = read('src/app/app.routes.ts');
const api = read('src/app/core/api/dashboard-api.service.ts');
const config = read('src/app/app.config.ts');

for (const route of ['overview', 'accounts', 'channels', 'widgets', 'polling']) {
  if (!routes.includes(`path: '${route}'`)) {
    throw new Error(`Missing dashboard route: ${route}`);
  }
}

for (const endpoint of [
  '/api/config/accounts',
  '/api/config/accounts/connected',
  '/api/config/channels',
  '/api/config/provider-connections',
  '/api/config/widgets',
  '/api/config/polling',
  '/api/auth/youtube/start',
  '/api/auth/youtube/relogin',
  '/api/auth/youtube/refresh',
  '/api/auth/youtube/sync',
  '/api/auth/youtube/disconnect'
]) {
  if (!api.includes(endpoint)) {
    throw new Error(`Missing API client endpoint: ${endpoint}`);
  }
}

const accountsPage = read('src/app/features/accounts/accounts.page.ts');
for (const action of ['Connect Google/YouTube account', 'Re-login', 'Refresh token', 'Sync channels', 'Disconnect']) {
  if (!accountsPage.includes(action)) {
    throw new Error(`Missing account action: ${action}`);
  }
}

for (const interceptor of ['apiBaseUrlInterceptor', 'httpErrorInterceptor']) {
  if (!config.includes(interceptor)) {
    throw new Error(`Missing HTTP interceptor registration: ${interceptor}`);
  }
}

console.log('Dashboard structure verified.');

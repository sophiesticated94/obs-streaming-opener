# OBS Streaming Opener

Local-first .NET Web API for OBS browser-source widgets. It stores stream events, channel metrics, audience relationships, provider cursors, and Hangfire jobs in one SQLite database.

## Requirements

- .NET 11 SDK `11.0.100-preview.5.26302.115`, pinned by `global.json`
- Node.js/npm compatible with Angular 22; current development setup uses Node `24.x` and npm `11.x`
- PowerShell examples below assume Windows
- Google OAuth client for YouTube account login
- YouTube Data API key is optional fallback for public video stats

## Projects

- `ObsStreamingOpener.Api` - Web API, health checks, Hangfire dashboard, static widgets
- `ObsStreamingOpener.Application` - use cases, provider orchestration, polling, Hangfire job classes
- `ObsStreamingOpener.Database.Model` - EF Core code-first entities
- `ObsStreamingOpener.Database` - `StreamingOpenerDbContext`, SQLite setup, repositories, initialization
- `ObsStreamingOpener.Domain` - provider-neutral enums
- `ObsStreamingOpener.Infrastructure` - clocks, HTTP clients, provider adapters/stubs
- `ObsStreamingOpener.Tests` - unit tests plus endpoint integration tests with EF InMemory

## Data Model

The app is channel-first:

- `MonitoredAccount` owns one or more `MonitoredChannel` records.
- `ProviderConnection`, `StreamEvent`, `MetricSnapshot`, `StreamSession`, and audience relationships belong to a `MonitoredChannel`.
- `ProviderCredential` belongs to a `MonitoredAccount` and stores encrypted OAuth tokens for provider operations.
- `StreamSession` is optional context, so events and metrics can exist during a stream or between streams.
- Audience terms are unified as `AudienceMember` and `AudienceRelationshipPeriod`; provider-specific names like subscriber, member, or patron stay in raw provider payloads/display labels.

## Run

```powershell
dotnet restore
dotnet build
dotnet test
npm --cache .\.npm-cache --prefix .\ObsStreamingOpener.Dashboard install
npm --cache .\.npm-cache --prefix .\ObsStreamingOpener.Dashboard run build
dotnet run --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
```

The dashboard uses Angular 22 and TypeScript 6. Keep using the workspace-local npm cache shown above.

Default local URL:

```text
http://localhost:5198
```

The first run creates:

```text
ObsStreamingOpener.Api\data\streaming-opener.db
ObsStreamingOpener.Api\data\keys\
```

The same SQLite file is used by EF Core application data and Hangfire storage. Data Protection keys in `data\keys` encrypt stored OAuth tokens.

Dashboard URL:

```text
http://localhost:5198/dashboard
```

## Configuration

Default `ObsStreamingOpener.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "StreamingOpener": "Data Source=data/streaming-opener.db"
  },
  "StreamingMonitor": {
    "EnableStreamDataPolling": false,
    "StreamDataPollingSeconds": 5
  },
  "YouTube": {
    "ApiKey": "",
    "BaseUrl": "https://www.googleapis.com/youtube/v3/"
  },
  "YouTubeOAuth": {
    "ClientId": "",
    "ClientSecret": "",
    "RedirectUri": "http://localhost:5198/api/auth/youtube/callback"
  }
}
```

Create a Google OAuth client in Google Cloud Console:

1. Enable YouTube Data API v3.
2. Create an OAuth client for a web application.
3. Add this authorized redirect URI:

```text
http://localhost:5198/api/auth/youtube/callback
```

Use user secrets for local YouTube OAuth config:

```powershell
dotnet user-secrets init --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "YouTubeOAuth:ClientId" "<google-oauth-client-id>" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "YouTubeOAuth:ClientSecret" "<google-oauth-client-secret>" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "YouTubeOAuth:RedirectUri" "http://localhost:5198/api/auth/youtube/callback" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "StreamingMonitor:EnableStreamDataPolling" "true" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
```

Optional API-key fallback for public video stats:

```powershell
dotnet user-secrets set "YouTube:ApiKey" "<youtube-data-api-key>" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
```

## Connect YouTube Account

1. Start the API.
2. Open `http://localhost:5198/dashboard/accounts`.
3. Click `Connect Google/YouTube account`.
4. Finish Google OAuth consent.
5. The callback stores encrypted credentials, discovers YouTube channels, and creates/updates `MonitoredChannel` plus `ProviderConnection` rows.
6. Use `Re-login` to replace tokens, `Refresh token` to refresh immediately, `Sync channels` to re-read YouTube channels, and `Disconnect` to clear credentials while preserving historical data.

The dashboard shows token state only: connected/expired/re-login/disconnected, expiry, refresh-token availability, and scopes. It never displays raw tokens.

For live YouTube stream monitoring, a `ProviderConnection` for the channel must contain:

- `ExternalStreamId` - YouTube video ID for viewer/like metric polling
- `ExternalChannelId` - YouTube live chat ID for chat polling

Connected YouTube channels are discovered from OAuth. Edit channel/provider connection details in the dashboard when you need to attach the current live stream/video IDs.

## Smoke Test With Sample Data

Start the API in Development, then create a sample event:

```powershell
Invoke-RestMethod -Method Post http://localhost:5198/api/dev/events/sample `
  -ContentType 'application/json' `
  -Body '{"provider":"Custom","eventType":"Tip","actorName":"Test viewer","message":"Great stream","amount":25,"currency":"PLN"}'
```

Create a sample audience relationship:

```powershell
Invoke-RestMethod -Method Post http://localhost:5198/api/dev/events/audience/sample `
  -ContentType 'application/json' `
  -Body '{"provider":"Custom","externalAudienceId":"demo-1","displayName":"Demo audience","relationshipKind":"Free"}'
```

Check data:

```powershell
Invoke-RestMethod http://localhost:5198/health
Invoke-RestMethod http://localhost:5198/api/channels
Invoke-RestMethod http://localhost:5198/api/events/recent
Invoke-RestMethod http://localhost:5198/api/stats/current
Invoke-RestMethod http://localhost:5198/api/widgets/stats/data
```

If you want channel-scoped URLs, copy the `id` from `GET /api/channels` and pass it as `channelId`.

## Dashboard

The Angular dashboard is a separate project in `ObsStreamingOpener.Dashboard` and is served by the API from `/dashboard`.

Build it into the API static folder:

```powershell
npm --cache .\.npm-cache --prefix .\ObsStreamingOpener.Dashboard install
npm --cache .\.npm-cache --prefix .\ObsStreamingOpener.Dashboard run build
```

Local Angular dev server:

```powershell
npm --cache .\.npm-cache --prefix .\ObsStreamingOpener.Dashboard start
```

The dashboard uses real API/database state only. Accounts are connected through Google OAuth, channels are discovered from logged-in accounts, and channel/provider/widget edits write back through `/api/config/*`. Polling settings are displayed from effective app configuration.

## OBS Widgets

Add a Browser Source in OBS and use one of these URLs:

```text
http://localhost:5198/widgets/stats.html
http://localhost:5198/widgets/recent-events.html
http://localhost:5198/widgets/audience.html
http://localhost:5198/widgets/goal.html?target=1000&label=Support%20goal
```

Useful query params:

```text
channelId=<guid>
theme=light
interval=2000
```

Example:

```text
http://localhost:5198/widgets/stats.html?channelId=<guid>&theme=light&interval=2000
```

## API Endpoints

Configuration:

- `GET/POST/PUT /api/config/accounts`
- `GET /api/config/accounts/connected`
- `GET/POST/PUT /api/config/channels`
- `GET/POST/PUT/DELETE /api/config/provider-connections`
- `GET/PUT /api/config/widgets`
- `GET /api/config/polling`

YouTube OAuth:

- `GET /api/auth/youtube/start?accountId=`
- `GET /api/auth/youtube/callback`
- `POST /api/auth/youtube/relogin/{accountId}`
- `POST /api/auth/youtube/refresh/{accountId}`
- `POST /api/auth/youtube/sync/{accountId}`
- `DELETE /api/auth/youtube/disconnect/{accountId}`

Channel/account:

- `GET /api/accounts`
- `GET /api/channels`
- `GET /api/channels/{channelId}`

Channel-scoped data:

- `GET /api/channels/{channelId}/events/recent?type=&limit=`
- `GET /api/channels/{channelId}/stats/current`
- `GET /api/channels/{channelId}/stats/summary?from=&to=`
- `GET /api/channels/{channelId}/audience/recent`
- `GET /api/channels/{channelId}/audience/{audienceMemberId}/history`
- `GET /api/channels/{channelId}/content/recent?kind=&limit=`
- `GET /api/channels/{channelId}/content/upcoming?limit=`
- `GET /api/channels/{channelId}/comments/recent?limit=`
- `GET /api/channels/{channelId}/youtube/overview`

Compatibility shortcuts use the default channel unless `channelId` is provided:

- `GET /api/streams/current`
- `GET /api/events/recent?channelId=&provider=&type=&limit=`
- `GET /api/stats/current?channelId=`
- `GET /api/stats/summary?channelId=&from=&to=`
- Widgets use small domain endpoints plus SignalR deltas. The generic `/api/widgets/{widgetKey}/data` endpoint has been removed.

Development-only:

- `POST /api/dev/events/sample`
- `POST /api/dev/events/audience/sample`

## Hangfire

- Dashboard: `http://localhost:5198/hangfire`
- Storage: the same SQLite database as the app, from `ConnectionStrings:StreamingOpener`
- Stream data polling runs every 5 seconds through the hosted service when enabled
- Account data polling is scheduled through Hangfire once per minute
- YouTube recurring jobs:
  - `youtube-account-summary-sync`: channel metadata and metrics every 5 minutes
  - `youtube-live-broadcast-sync`: active/upcoming/completed broadcasts every minute
  - `youtube-content-discovery-sync`: uploads and channel activities every 15 minutes
  - `youtube-subscriber-sync`: visible subscribers every 30 minutes
- Hangfire job classes and registration live in `ObsStreamingOpener.Application`
- API only wires Hangfire storage/server/dashboard

## YouTube Data Collection

The app uses the connected Google/YouTube OAuth account with the read-only YouTube scope. It stores provider-neutral content in `ProviderResource`, appends metric snapshots only when values change, and stores every non-duplicate event.

Collected data includes:

- Channel metadata, audience count, total views, video count, and uploads playlist
- Uploaded videos and video details such as views, likes, comments, publish time, and raw payload JSON
- Live broadcasts and live streams, including scheduled/start/end times and status
- Channel activities as content events
- Recent comments as `CommentCreated` events
- Visible subscribers as best-effort `AudienceRelationshipStarted` events; YouTube may hide or limit subscriber identities, so the reliable source is the audience count metric

## Tipply Support Sync

Tipply support sync uses a browser session, not stored login/password.

1. Configure:
   - `SupportProviders:Tipply:Enabled=true`
   - `SupportProviders:Tipply:BaseUrl=https://proxy.tipply.pl`
   - `SupportProviders:Tipply:LoginUrl=https://tipply.pl/login`
2. Open dashboard revenue provider actions and run browser login for Tipply.
3. Log in manually in the headful browser and close the browser window when finished.
4. The app stores encrypted browser storage state in SQLite through Data Protection.
5. Run `POST /api/revenue/sync` or the dashboard sync action.

The crawler reads `GET https://proxy.tipply.pl/user/tips?limit=50&offset=0&filter=undefined&search=undefined`, pages until it reaches an empty page or the last cursor, maps records into `StreamEventType.Tip` plus `Tip`, and reports `NeedsLogin` when the session expires.

## Adding A Provider

1. Add the provider client/wrapper in `ObsStreamingOpener.Infrastructure`.
2. Implement `IStreamingProviderMonitor` or `IAccountProviderMonitor`.
3. Normalize provider data into `ProviderEvent`, `ProviderAudienceRelationship`, or metric snapshots.
4. Store page tokens/cursors through `IProviderCursorStore`.
5. For support providers, implement `ISupportProviderAdapter` and return `ProviderTipRecord` / `ProviderPatronRecord`.
6. Store page tokens/cursors through `IProviderCursorStore`.
7. Register stream-scoped monitors as `IStreamingProviderMonitor`, account-scoped monitors as `IAccountProviderMonitor`, and support adapters as `ISupportProviderAdapter`.

Patronite and Zrzutka are currently registered as stubs. Tipply has a Playwright-backed browser crawler.

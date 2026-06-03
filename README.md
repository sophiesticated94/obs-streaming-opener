# OBS Streaming Opener

Local-first .NET Web API for collecting streaming events, channel metrics, and audience relationships, storing them in SQLite, and serving browser-source widgets for OBS.

## Architecture

- `ObsStreamingOpener.Api` hosts the Web API, health checks, Hangfire dashboard, and static OBS widgets.
- `ObsStreamingOpener.Application` contains provider orchestration, event ingestion, audience ingestion, stats queries, Hangfire job classes, and polling services.
- `ObsStreamingOpener.Domain` contains provider-neutral enums and value types.
- `ObsStreamingOpener.Database.Model` contains EF Core code-first entity models with attributes.
- `ObsStreamingOpener.Database` owns `StreamingOpenerDbContext`, SQLite registration, initialization, and repository/query implementations.
- `ObsStreamingOpener.Infrastructure` contains common implementations behind interfaces, including clocks, HTTP clients, YouTube API access, and provider stubs.
- `ObsStreamingOpener.Tests` covers channel-scoped event ingestion, cursor state, stats aggregation, and audience relationship renewal.

## Domain Model

The app is channel-first:

- `MonitoredAccount`: local creator/profile in this app.
- `MonitoredChannel`: provider channel/profile owned by a monitored account.
- `ProviderConnection`: technical connection/configuration for a monitored channel.
- `ProviderCursor`: last-read sync state for a provider connection.
- `StreamSession`: optional live-stream context for a monitored channel.
- `StreamEvent`: event owned by a monitored channel, optionally linked to a stream session.
- `MetricSnapshot`: point-in-time metric owned by a monitored channel, optionally linked to a stream session.
- `AudienceMember`: one known audience identity from a provider.
- `AudienceRelationshipPeriod`: one free or paid relationship period between an audience member and a channel.

Provider-specific terms stay at the adapter/display edge. Internally we use audience terms:

- free relationship: lightweight channel relationship;
- paid relationship: recurring paid/support relationship when a provider exposes it;
- `AudienceMemberCount` and `PaidAudienceMemberCount` for metric snapshots.

## Run Locally

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
```

The API defaults to local SQLite files under `ObsStreamingOpener.Api/data/`.

Useful URLs:

- Health: `http://localhost:5198/health`
- OpenAPI: `http://localhost:5198/openapi/v1.json` in Development
- Hangfire: `http://localhost:5198/hangfire`
- Stats widget: `http://localhost:5198/widgets/stats.html`
- Recent events widget: `http://localhost:5198/widgets/recent-events.html`
- Audience widget: `http://localhost:5198/widgets/audience.html`
- Goal widget: `http://localhost:5198/widgets/goal.html?target=1000&label=Support%20goal`

OBS can use those widget URLs as Browser Sources. Query parameters include `channelId=...`, `theme=light`, and `interval=2000`.

## Configuration

`appsettings.json` contains local defaults:

```json
{
  "ConnectionStrings": {
    "StreamingOpener": "Data Source=data/streaming-opener.db",
    "Hangfire": "Data Source=data/hangfire.db"
  },
  "StreamingMonitor": {
    "EnableYouTubePolling": false,
    "YouTubeMetricPollingSeconds": 10
  },
  "YouTube": {
    "ApiKey": "",
    "BaseUrl": "https://www.googleapis.com/youtube/v3/"
  }
}
```

Set the YouTube API key with user secrets or environment variables:

```powershell
dotnet user-secrets init --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "YouTube:ApiKey" "<your-api-key>" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "StreamingMonitor:EnableYouTubePolling" "true" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
```

For YouTube connections:

- `ProviderConnection.ExternalStreamId`: video ID for viewer/like metrics.
- `ProviderConnection.ExternalChannelId`: live chat ID for chat polling.

## API Endpoints

- `GET /api/accounts`
- `GET /api/channels`
- `GET /api/channels/{channelId}`
- `GET /api/channels/{channelId}/events/recent?type=&limit=`
- `GET /api/channels/{channelId}/stats/current`
- `GET /api/channels/{channelId}/stats/summary?from=&to=`
- `GET /api/channels/{channelId}/audience/recent`
- `GET /api/channels/{channelId}/audience/{audienceMemberId}/history`

Compatibility shortcuts use the default channel:

- `GET /api/streams/current`
- `GET /api/events/recent?channelId=&provider=&type=&limit=`
- `GET /api/stats/current?channelId=`
- `GET /api/stats/summary?channelId=&from=&to=`
- `GET /api/widgets/{widgetKey}/data?channelId=`

Development-only samples:

```powershell
Invoke-RestMethod -Method Post http://localhost:5198/api/dev/events/sample `
  -ContentType 'application/json' `
  -Body '{"provider":"Custom","eventType":"Tip","actorName":"Test viewer","message":"Great stream","amount":25,"currency":"PLN"}'

Invoke-RestMethod -Method Post http://localhost:5198/api/dev/events/audience/sample `
  -ContentType 'application/json' `
  -Body '{"provider":"Custom","externalAudienceId":"demo-1","displayName":"Demo audience","relationshipKind":"Free"}'
```

## Adding Providers

1. Add a provider client in `ObsStreamingOpener.Infrastructure`.
2. Implement `IStreamingProviderMonitor` or `ITipProviderMonitor`.
3. Normalize external payloads into `ProviderEvent` or `ProviderAudienceRelationship`.
4. Persist cursors through `IProviderCursorStore`.
5. Register the monitor as `IProviderMonitor` in infrastructure DI.

Tipply and Patronite are registered as stubs so they already appear in the provider orchestration path.

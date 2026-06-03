# OBS Streaming Opener

Local-first .NET Web API for collecting streaming events and metrics, storing them in SQLite, and serving browser-source widgets for OBS.

## Architecture

- `ObsStreamingOpener.Api` hosts the Web API, health checks, Hangfire dashboard, and static OBS widgets.
- `ObsStreamingOpener.Application` contains provider orchestration, event ingestion, stats queries, Hangfire job classes, and polling services.
- `ObsStreamingOpener.Domain` contains provider-neutral enums and value types.
- `ObsStreamingOpener.Database.Model` contains EF Core code-first entity models with attributes.
- `ObsStreamingOpener.Database` owns `StreamingOpenerDbContext`, SQLite registration, initialization, and repository/query implementations.
- `ObsStreamingOpener.Infrastructure` contains common implementations behind interfaces, including clocks, HTTP clients, YouTube API access, and provider stubs.
- `ObsStreamingOpener.Tests` covers ingestion, cursor state, stats aggregation, and API startup.

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
- Goal widget: `http://localhost:5198/widgets/goal.html?target=1000&label=Support%20goal`

OBS can use those widget URLs as Browser Sources. Query parameters include `theme=light` and `interval=2000`.

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

For YouTube connections, the current skeleton expects:

- `ProviderConnection.ExternalStreamId`: YouTube video ID for viewer/like metrics.
- `ProviderConnection.ExternalChannelId`: YouTube live chat ID for chat polling.

## API Endpoints

- `GET /api/streams/current`
- `GET /api/events/recent?provider=&type=&limit=`
- `GET /api/stats/current`
- `GET /api/stats/summary?from=&to=`
- `GET /api/widgets/{widgetKey}/data`
- `POST /api/dev/events/sample` in Development only

Sample development event:

```powershell
Invoke-RestMethod -Method Post http://localhost:5198/api/dev/events/sample `
  -ContentType 'application/json' `
  -Body '{"provider":"Custom","eventType":"Tip","actorName":"Test viewer","message":"Great stream","amount":25,"currency":"PLN"}'
```

## Adding Providers

1. Add a provider client in `ObsStreamingOpener.Infrastructure`.
2. Implement `IStreamingProviderMonitor` or `ITipProviderMonitor`.
3. Normalize external payloads into `ProviderEvent`.
4. Persist cursors through `IProviderCursorStore`.
5. Register the monitor as `IProviderMonitor` in infrastructure DI.

Tipply and Patronite are registered as stubs so they already appear in the provider orchestration path.

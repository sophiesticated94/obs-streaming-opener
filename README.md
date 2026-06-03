# OBS Streaming Opener

Local-first .NET Web API for OBS browser-source widgets. It stores stream events, channel metrics, audience relationships, provider cursors, and Hangfire jobs in one SQLite database.

## Requirements

- .NET SDK pinned by `global.json`
- PowerShell examples below assume Windows
- YouTube Data API key only if real provider polling is enabled

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
- `StreamSession` is optional context, so events and metrics can exist during a stream or between streams.
- Audience terms are unified as `AudienceMember` and `AudienceRelationshipPeriod`; provider-specific names like subscriber, member, or patron stay in raw provider payloads/display labels.

## Run

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
```

Default local URL:

```text
http://localhost:5198
```

The first run creates:

```text
ObsStreamingOpener.Api\data\streaming-opener.db
```

The same SQLite file is used by EF Core application data and Hangfire storage.

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
  }
}
```

Use user secrets for local YouTube config:

```powershell
dotnet user-secrets init --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "YouTube:ApiKey" "<your-api-key>" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
dotnet user-secrets set "StreamingMonitor:EnableStreamDataPolling" "true" --project .\ObsStreamingOpener.Api\ObsStreamingOpener.Api.csproj
```

For real YouTube monitoring, a `ProviderConnection` for the channel must contain:

- `ExternalStreamId` - YouTube video ID for viewer/like metric polling
- `ExternalChannelId` - YouTube live chat ID for chat polling

The current skeleton seeds a default account/channel automatically. Admin endpoints for managing provider connections are not implemented yet.

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

Compatibility shortcuts use the default channel unless `channelId` is provided:

- `GET /api/streams/current`
- `GET /api/events/recent?channelId=&provider=&type=&limit=`
- `GET /api/stats/current?channelId=`
- `GET /api/stats/summary?channelId=&from=&to=`
- `GET /api/widgets/{widgetKey}/data?channelId=`

Development-only:

- `POST /api/dev/events/sample`
- `POST /api/dev/events/audience/sample`

## Hangfire

- Dashboard: `http://localhost:5198/hangfire`
- Storage: the same SQLite database as the app, from `ConnectionStrings:StreamingOpener`
- Stream data polling runs every 5 seconds through the hosted service when enabled
- Account data polling is scheduled through Hangfire once per minute
- Hangfire job classes and registration live in `ObsStreamingOpener.Application`
- API only wires Hangfire storage/server/dashboard

## Adding A Provider

1. Add the provider client/wrapper in `ObsStreamingOpener.Infrastructure`.
2. Implement `IStreamingProviderMonitor` or `ITipProviderMonitor`.
3. Normalize provider data into `ProviderEvent`, `ProviderAudienceRelationship`, or metric snapshots.
4. Store page tokens/cursors through `IProviderCursorStore`.
5. Register stream-scoped monitors as `IStreamingProviderMonitor` and account-scoped monitors as `ITipProviderMonitor` or a future account monitor contract.

Tipply and Patronite are currently registered as stubs.

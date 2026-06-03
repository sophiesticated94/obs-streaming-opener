using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Options;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.HostedServices;

public sealed class YouTubeMetricPollingService(
    IServiceScopeFactory scopeFactory,
    IOptions<StreamingMonitorOptions> options,
    ILogger<YouTubeMetricPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.EnableYouTubePolling)
        {
            logger.LogInformation("YouTube high-frequency polling is disabled.");
            return;
        }

        var intervalSeconds = Math.Max(5, settings.YouTubeMetricPollingSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollYouTubeMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "YouTube metric polling failed.");
            }
        }
    }

    private async Task PollYouTubeMetricsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var channelStore = scope.ServiceProvider.GetRequiredService<IChannelStore>();
        var sessionStore = scope.ServiceProvider.GetRequiredService<IStreamSessionStore>();
        var statsStore = scope.ServiceProvider.GetRequiredService<IStatsStore>();
        var client = scope.ServiceProvider.GetRequiredService<IYouTubeApiClient>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var connections = await channelStore.GetEnabledConnectionsAsync(ProviderKind.YouTube, cancellationToken);
        foreach (var connection in connections.Where(x => !string.IsNullOrWhiteSpace(x.ExternalStreamId)))
        {
            var stats = await client.GetViewerStatsAsync(connection.ExternalStreamId!, cancellationToken);
            if (stats is null)
            {
                continue;
            }

            if (stats.ConcurrentViewers.HasValue)
            {
                await statsStore.AddMetricSnapshotAsync(new MetricSnapshot
                {
                    MonitoredChannelId = connection.MonitoredChannelId,
                    StreamSessionId = (await sessionStore.GetCurrentSessionAsync(connection.MonitoredChannelId, cancellationToken))?.Id,
                    ProviderConnectionId = connection.Id,
                    Provider = ProviderKind.YouTube,
                    Metric = MetricKind.ConcurrentViewers,
                    SnapshotReason = SnapshotReason.ScheduledPoll,
                    Value = stats.ConcurrentViewers.Value,
                    Unit = "viewers",
                    CapturedAt = clock.UtcNow,
                    RawPayloadJson = stats.RawPayloadJson
                }, cancellationToken);
            }

            if (stats.Likes.HasValue)
            {
                await statsStore.AddMetricSnapshotAsync(new MetricSnapshot
                {
                    MonitoredChannelId = connection.MonitoredChannelId,
                    StreamSessionId = (await sessionStore.GetCurrentSessionAsync(connection.MonitoredChannelId, cancellationToken))?.Id,
                    ProviderConnectionId = connection.Id,
                    Provider = ProviderKind.YouTube,
                    Metric = MetricKind.Likes,
                    SnapshotReason = SnapshotReason.ScheduledPoll,
                    Value = stats.Likes.Value,
                    Unit = "likes",
                    CapturedAt = clock.UtcNow,
                    RawPayloadJson = stats.RawPayloadJson
                }, cancellationToken);
            }
        }
    }
}

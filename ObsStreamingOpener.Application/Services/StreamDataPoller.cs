using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Exceptions;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class StreamDataPoller(
    IChannelStore channelStore,
    IStreamSessionStore sessionStore,
    IStatsStore statsStore,
    IYouTubeApiClient youtubeClient,
    IYouTubeCredentialResolver youtubeCredentialResolver,
    IClock clock,
    IEnumerable<IStreamingProviderMonitor> streamMonitors,
    ILogger<StreamDataPoller> logger) : IStreamDataPoller
{
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        await PollYouTubeStreamMetricsAsync(cancellationToken);

        foreach (var monitor in streamMonitors)
        {
            try
            {
                await monitor.PollAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stream data monitor {ProviderMonitor} failed.", monitor.Name);
            }
        }
    }

    private async Task PollYouTubeStreamMetricsAsync(CancellationToken cancellationToken)
    {
        var connections = await channelStore.GetEnabledConnectionsAsync(ProviderKind.YouTube, cancellationToken);
        foreach (var connection in connections.Where(x => !string.IsNullOrWhiteSpace(x.ExternalStreamId)))
        {
            var currentSession = await sessionStore.GetCurrentSessionAsync(connection.MonitoredChannelId, cancellationToken);
            if (currentSession is null)
            {
                continue;
            }

            YouTubeViewerStats? stats;
            try
            {
                var credential = await youtubeCredentialResolver.ResolveForChannelAsync(connection.MonitoredChannelId, cancellationToken);
                stats = await youtubeClient.GetViewerStatsAsync(connection.ExternalStreamId!, credential?.AccessToken, cancellationToken);
            }
            catch (ExternalHttpRequestException ex)
            {
                logger.LogWarning(
                    ex,
                    "YouTube stream metric poll failed for connection {ProviderConnectionId}: {StatusCode} {ProviderErrorCode} {ProviderErrorMessage}. Response: {ResponseBody}",
                    connection.Id,
                    (int)ex.StatusCode,
                    ex.ProviderErrorCode,
                    ex.ProviderErrorMessage,
                    ex.ResponseBody);
                continue;
            }

            if (stats is null)
            {
                continue;
            }

            if (stats.ConcurrentViewers.HasValue)
            {
                await statsStore.AddMetricSnapshotIfChangedAsync(CreateSnapshot(
                    connection,
                    currentSession,
                    MetricKind.ConcurrentViewers,
                    stats.ConcurrentViewers.Value,
                    "viewers",
                    stats.RawPayloadJson), cancellationToken);
            }

            if (stats.Likes.HasValue)
            {
                await statsStore.AddMetricSnapshotIfChangedAsync(CreateSnapshot(
                    connection,
                    currentSession,
                    MetricKind.Likes,
                    stats.Likes.Value,
                    "likes",
                    stats.RawPayloadJson), cancellationToken);
            }
        }
    }

    private MetricSnapshot CreateSnapshot(
        ProviderConnectionDto connection,
        StreamSessionDto currentSession,
        MetricKind metric,
        decimal value,
        string unit,
        string rawPayloadJson)
        => new()
        {
            MonitoredChannelId = connection.MonitoredChannelId,
            StreamSessionId = currentSession.Id,
            ProviderConnectionId = connection.Id,
            ProviderResourceId = currentSession.ProviderResourceId,
            Provider = ProviderKind.YouTube,
            Metric = metric,
            SnapshotReason = SnapshotReason.ScheduledPoll,
            Value = value,
            Unit = unit,
            CapturedAt = clock.UtcNow,
            RawPayloadJson = rawPayloadJson
        };
}

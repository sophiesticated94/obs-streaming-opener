using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class StatsQueryService(
    IStatsStore statsStore,
    IEventStore eventStore,
    IChannelStore channelStore,
    IProviderResourceStore resourceStore,
    IClock clock) : IStatsQueryService
{
    public async Task<CurrentStatsDto> GetCurrentStatsAsync(Guid? monitoredChannelId = null, CancellationToken cancellationToken = default)
    {
        var channel = monitoredChannelId.HasValue
            ? await channelStore.GetChannelAsync(monitoredChannelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);

        if (channel is null)
        {
            throw new InvalidOperationException("Monitored channel was not found.");
        }

        var currentStream = await statsStore.GetCurrentStreamAsync(channel.Id, cancellationToken);
        var viewers = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.ConcurrentViewers, cancellationToken);
        var likes = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.Likes, cancellationToken);
        var chatRate = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.ChatMessagesPerMinute, cancellationToken);
        var tipTotal = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.TipTotal, cancellationToken);
        var audience = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.AudienceMemberCount, cancellationToken);
        var paidAudience = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.PaidAudienceMemberCount, cancellationToken);
        var lastUpdated = new[] { viewers?.CapturedAt, likes?.CapturedAt, chatRate?.CapturedAt, tipTotal?.CapturedAt, audience?.CapturedAt, paidAudience?.CapturedAt }
            .Where(x => x.HasValue)
            .Max();

        return new CurrentStatsDto(
            channel.Id,
            channel.DisplayName,
            currentStream?.Id,
            currentStream?.Title,
            viewers?.Value ?? 0,
            likes?.Value ?? 0,
            chatRate?.Value ?? 0,
            tipTotal?.Value ?? 0,
            audience?.Value ?? 0,
            paidAudience?.Value ?? 0,
            lastUpdated);
    }

    public async Task<StatsSummaryDto> GetSummaryAsync(
        Guid? monitoredChannelId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var channel = monitoredChannelId.HasValue
            ? await channelStore.GetChannelAsync(monitoredChannelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);

        if (channel is null)
        {
            throw new InvalidOperationException("Monitored channel was not found.");
        }

        var toValue = to ?? clock.UtcNow;
        var fromValue = from ?? toValue.AddHours(-4);
        var metrics = await statsStore.GetMetricsAsync(channel.Id, fromValue, toValue, providerResourceId, streamSessionId, cancellationToken);
        var viewerMetrics = metrics.Where(x => x.Metric == MetricKind.ConcurrentViewers).ToArray();
        var tipMetrics = metrics.Where(x => x.Metric == MetricKind.TipTotal).ToArray();
        var chatEvents = await eventStore.GetRecentEventsAsync(channel.Id, null, StreamEventType.ChatMessage, 10_000, providerResourceId, streamSessionId, null, cancellationToken);
        var allEvents = await eventStore.GetRecentEventsAsync(channel.Id, null, null, 10_000, providerResourceId, streamSessionId, null, cancellationToken);

        return new StatsSummaryDto(
            fromValue,
            toValue,
            viewerMetrics.Length == 0 ? 0 : viewerMetrics.Max(x => x.Value),
            viewerMetrics.Length == 0 ? 0 : decimal.Round(viewerMetrics.Average(x => x.Value), 2),
            chatEvents.Count(x => x.OccurredAt >= fromValue && x.OccurredAt <= toValue),
            tipMetrics.Length == 0 ? 0 : tipMetrics.Max(x => x.Value),
            allEvents.Count(x => x.OccurredAt >= fromValue && x.OccurredAt <= toValue));
    }

    public async Task<WidgetDataDto> GetWidgetDataAsync(string widgetKey, Guid? monitoredChannelId = null, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var channel = monitoredChannelId.HasValue
            ? await channelStore.GetChannelAsync(monitoredChannelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);

        if (channel is null)
        {
            throw new InvalidOperationException("Monitored channel was not found.");
        }

        var current = await GetCurrentStatsAsync(channel.Id, cancellationToken);
        var summary = await GetSummaryAsync(channel.Id, now.AddHours(-4), now, cancellationToken: cancellationToken);
        var recent = await eventStore.GetRecentEventsAsync(channel.Id, null, null, 20, cancellationToken: cancellationToken);
        var content = await resourceStore.GetRecentResourcesAsync(channel.Id, null, 10, cancellationToken);
        var upcoming = await resourceStore.GetUpcomingResourcesAsync(channel.Id, 5, cancellationToken);
        var comments = await eventStore.GetRecentEventsAsync(channel.Id, ProviderKind.YouTube, StreamEventType.CommentCreated, 10, cancellationToken: cancellationToken);

        return new WidgetDataDto(widgetKey, channel.Id, current, summary, recent, now, content, upcoming, comments);
    }
}

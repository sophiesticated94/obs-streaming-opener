using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class StatsQueryService(
    IStatsStore statsStore,
    IEventStore eventStore,
    IChannelStore channelStore,
    IClock clock) : IStatsQueryService
{
    public Task<CurrentStatsDto> GetCurrentStatsAsync(Guid? monitoredChannelId = null, CancellationToken cancellationToken = default)
        => GetCurrentStatsAsync(monitoredChannelId, null, null, cancellationToken);

    public async Task<CurrentStatsDto> GetCurrentStatsAsync(
        Guid? monitoredChannelId,
        Guid? providerResourceId,
        Guid? streamSessionId,
        CancellationToken cancellationToken = default)
    {
        var channel = monitoredChannelId.HasValue
            ? await channelStore.GetChannelAsync(monitoredChannelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);

        if (channel is null)
        {
            throw new InvalidOperationException("Monitored channel was not found.");
        }

        var currentStream = await statsStore.GetCurrentStreamAsync(channel.Id, cancellationToken);
        var viewers = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.ConcurrentViewers, providerResourceId, streamSessionId, cancellationToken);
        var likes = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.Likes, providerResourceId, streamSessionId, cancellationToken);
        var chatRate = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.ChatMessagesPerMinute, providerResourceId, streamSessionId, cancellationToken);
        var tipTotal = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.TipTotal, providerResourceId, streamSessionId, cancellationToken);
        var audience = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.AudienceMemberCount, providerResourceId, streamSessionId, cancellationToken);
        var paidAudience = await statsStore.GetLatestMetricAsync(channel.Id, MetricKind.PaidAudienceMemberCount, providerResourceId, streamSessionId, cancellationToken);
        var scopedMetrics = new[] { viewers, likes, chatRate, tipTotal, audience, paidAudience };
        var resolvedStreamSessionId = streamSessionId
            ?? scopedMetrics.FirstOrDefault(x => x?.StreamSessionId is not null)?.StreamSessionId
            ?? (providerResourceId.HasValue ? null : currentStream?.Id);
        var lastUpdated = new[] { viewers?.CapturedAt, likes?.CapturedAt, chatRate?.CapturedAt, tipTotal?.CapturedAt, audience?.CapturedAt, paidAudience?.CapturedAt }
            .Where(x => x.HasValue)
            .Max();

        return new CurrentStatsDto(
            channel.Id,
            channel.DisplayName,
            resolvedStreamSessionId,
            resolvedStreamSessionId == currentStream?.Id ? currentStream?.Title : null,
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

}

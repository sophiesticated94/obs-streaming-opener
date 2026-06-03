using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class StatsQueryService(
    IStatsStore statsStore,
    IEventStore eventStore,
    IClock clock) : IStatsQueryService
{
    public async Task<CurrentStatsDto> GetCurrentStatsAsync(CancellationToken cancellationToken = default)
    {
        var currentStream = await statsStore.GetCurrentStreamAsync(cancellationToken);
        var viewers = await statsStore.GetLatestMetricAsync(MetricKind.ConcurrentViewers, cancellationToken);
        var likes = await statsStore.GetLatestMetricAsync(MetricKind.Likes, cancellationToken);
        var chatRate = await statsStore.GetLatestMetricAsync(MetricKind.ChatMessagesPerMinute, cancellationToken);
        var tipTotal = await statsStore.GetLatestMetricAsync(MetricKind.TipTotal, cancellationToken);
        var lastUpdated = new[] { viewers?.CapturedAt, likes?.CapturedAt, chatRate?.CapturedAt, tipTotal?.CapturedAt }
            .Where(x => x.HasValue)
            .Max();

        return new CurrentStatsDto(
            currentStream?.Id,
            currentStream?.Title,
            viewers?.Value ?? 0,
            likes?.Value ?? 0,
            chatRate?.Value ?? 0,
            tipTotal?.Value ?? 0,
            lastUpdated);
    }

    public async Task<StatsSummaryDto> GetSummaryAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var toValue = to ?? clock.UtcNow;
        var fromValue = from ?? toValue.AddHours(-4);
        var metrics = await statsStore.GetMetricsAsync(fromValue, toValue, cancellationToken);
        var viewerMetrics = metrics.Where(x => x.Metric == MetricKind.ConcurrentViewers).ToArray();
        var tipMetrics = metrics.Where(x => x.Metric == MetricKind.TipTotal).ToArray();
        var chatEvents = await eventStore.GetRecentEventsAsync(null, StreamEventType.ChatMessage, 10_000, cancellationToken);
        var allEvents = await eventStore.GetRecentEventsAsync(null, null, 10_000, cancellationToken);

        return new StatsSummaryDto(
            fromValue,
            toValue,
            viewerMetrics.Length == 0 ? 0 : viewerMetrics.Max(x => x.Value),
            viewerMetrics.Length == 0 ? 0 : decimal.Round(viewerMetrics.Average(x => x.Value), 2),
            chatEvents.Count(x => x.OccurredAt >= fromValue && x.OccurredAt <= toValue),
            tipMetrics.Length == 0 ? 0 : tipMetrics.Max(x => x.Value),
            allEvents.Count(x => x.OccurredAt >= fromValue && x.OccurredAt <= toValue));
    }

    public async Task<WidgetDataDto> GetWidgetDataAsync(string widgetKey, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var current = await GetCurrentStatsAsync(cancellationToken);
        var summary = await GetSummaryAsync(now.AddHours(-4), now, cancellationToken);
        var recent = await eventStore.GetRecentEventsAsync(null, null, 20, cancellationToken);

        return new WidgetDataDto(widgetKey, current, summary, recent, now);
    }
}

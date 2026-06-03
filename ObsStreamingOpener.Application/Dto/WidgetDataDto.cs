namespace ObsStreamingOpener.Application.Dto;

public sealed record WidgetDataDto(
    string WidgetKey,
    Guid MonitoredChannelId,
    CurrentStatsDto CurrentStats,
    StatsSummaryDto Summary,
    IReadOnlyList<RecentEventDto> RecentEvents,
    DateTimeOffset GeneratedAt);

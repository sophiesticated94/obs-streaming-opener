namespace ObsStreamingOpener.Application.Dto;

public sealed record WidgetDataDto(
    string WidgetKey,
    CurrentStatsDto CurrentStats,
    StatsSummaryDto Summary,
    IReadOnlyList<RecentEventDto> RecentEvents,
    DateTimeOffset GeneratedAt);

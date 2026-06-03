namespace ObsStreamingOpener.Application.Dto;

public sealed record StatsSummaryDto(
    DateTimeOffset From,
    DateTimeOffset To,
    decimal PeakViewers,
    decimal AverageViewers,
    int ChatMessages,
    decimal TipTotal,
    int EventCount);

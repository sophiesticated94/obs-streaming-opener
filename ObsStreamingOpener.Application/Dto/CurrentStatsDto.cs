namespace ObsStreamingOpener.Application.Dto;

public sealed record CurrentStatsDto(
    Guid? StreamSessionId,
    string? StreamTitle,
    decimal ConcurrentViewers,
    decimal Likes,
    decimal ChatMessagesPerMinute,
    decimal TipTotal,
    DateTimeOffset? LastUpdatedAt);

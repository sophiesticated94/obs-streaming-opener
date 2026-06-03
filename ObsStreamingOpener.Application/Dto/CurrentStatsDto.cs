namespace ObsStreamingOpener.Application.Dto;

public sealed record CurrentStatsDto(
    Guid MonitoredChannelId,
    string ChannelDisplayName,
    Guid? StreamSessionId,
    string? StreamTitle,
    decimal ConcurrentViewers,
    decimal Likes,
    decimal ChatMessagesPerMinute,
    decimal TipTotal,
    decimal AudienceMemberCount,
    decimal PaidAudienceMemberCount,
    DateTimeOffset? LastUpdatedAt);

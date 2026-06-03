namespace ObsStreamingOpener.Application.Dto;

public sealed record StreamSessionDto(
    Guid Id,
    Guid MonitoredChannelId,
    string Title,
    bool IsActive,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

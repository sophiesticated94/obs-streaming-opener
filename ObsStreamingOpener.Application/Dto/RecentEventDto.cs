using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record RecentEventDto(
    Guid Id,
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    Guid? AudienceMemberId,
    Guid? ProviderResourceId,
    ProviderKind Provider,
    StreamEventType EventType,
    string? ActorName,
    string? Title,
    string? Message,
    decimal? Amount,
    string? Currency,
    DateTimeOffset OccurredAt);

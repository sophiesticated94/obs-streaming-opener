using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderEvent(
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    Guid? AudienceMemberId,
    Guid? ProviderResourceId,
    ProviderKind Provider,
    StreamEventType EventType,
    string? ExternalEventId,
    string? ActorName,
    string? ActorExternalId,
    string? Title,
    string? Message,
    decimal? Amount,
    string? Currency,
    DateTimeOffset OccurredAt,
    string? RawPayloadJson,
    string? IdentityKey = null,
    decimal? Value = null,
    string? Unit = null,
    string? ContextJson = null);

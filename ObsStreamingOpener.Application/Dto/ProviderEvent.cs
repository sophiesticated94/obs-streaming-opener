using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderEvent(
    Guid StreamSessionId,
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
    string? RawPayloadJson);

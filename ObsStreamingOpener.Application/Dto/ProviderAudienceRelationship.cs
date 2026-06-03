using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderAudienceRelationship(
    Guid MonitoredChannelId,
    ProviderKind Provider,
    string ExternalAudienceId,
    string? DisplayName,
    string? ProfileUrl,
    AudienceRelationshipKind RelationshipKind,
    DateTimeOffset StartedAt,
    bool IsEstimated,
    string? RawPayloadJson);

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
    string? RawPayloadJson,
    string? SupportExternalId = null,
    string? TierName = null,
    RelationshipStatus Status = RelationshipStatus.Active,
    BillingCadence BillingCadence = BillingCadence.Unknown,
    decimal? Amount = null,
    string? Currency = null,
    DateTimeOffset? LastChargeAt = null,
    DateTimeOffset? NextChargeAt = null,
    DateTimeOffset? CancelledAt = null);

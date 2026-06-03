using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record AudienceRelationshipPeriodDto(
    Guid Id,
    Guid MonitoredChannelId,
    Guid AudienceMemberId,
    AudienceRelationshipKind RelationshipKind,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    bool IsEstimated,
    string? AudienceDisplayName);

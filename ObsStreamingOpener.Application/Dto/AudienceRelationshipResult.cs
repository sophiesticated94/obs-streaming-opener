namespace ObsStreamingOpener.Application.Dto;

public sealed record AudienceRelationshipResult(
    Guid AudienceMemberId,
    Guid RelationshipPeriodId,
    bool CreatedAudienceMember,
    bool CreatedRelationshipPeriod,
    bool Renewed);

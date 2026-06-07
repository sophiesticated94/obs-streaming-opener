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
    string? AudienceDisplayName,
    bool IsPatron = false,
    string? PatronTierName = null,
    decimal? LatestCurrencyTotal = null,
    string? LatestCurrency = null,
    IReadOnlyList<AudienceCurrencyTotalDto>? CurrencyTotals = null,
    DateTimeOffset? LastActivityAt = null);

public sealed record AudienceCurrencyTotalDto(string Currency, decimal Total);

public sealed record AudienceActivityDto(
    Guid AudienceMemberId,
    IReadOnlyList<AudienceRelationshipPeriodDto> Relationships,
    IReadOnlyList<RecentEventDto> Events,
    IReadOnlyList<ProviderMessageDto> Messages,
    AudienceRevenueSummaryDto Revenue);

public sealed record AudienceRevenueSummaryDto(
    decimal? LatestCurrencyTotal,
    string? LatestCurrency,
    IReadOnlyList<AudienceCurrencyTotalDto> CurrencyTotals);

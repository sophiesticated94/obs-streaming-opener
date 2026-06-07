using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderTipRecord(
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    ProviderKind Provider,
    TipKind TipKind,
    TipStatus Status,
    TipSource Source,
    PaymentMethod PaymentMethod,
    string? ExternalTipId,
    string? RefundedExternalTipId,
    string? ActorName,
    string? ActorExternalId,
    decimal Amount,
    string Currency,
    decimal? GrossAmount,
    decimal? KnownNetAmount,
    decimal? EstimatedNetAmount,
    IReadOnlyList<FeeLine> FeeLines,
    string? Message,
    DateTimeOffset OccurredAt,
    string? CampaignExternalId,
    string? PayoutExternalId,
    string? SupportExternalId,
    string? PatronTierName,
    string? ContextJson,
    bool IsSyntheticExternalId = false);

public sealed record ProviderPatronRecord(
    Guid MonitoredChannelId,
    ProviderKind Provider,
    string ExternalAudienceId,
    string? DisplayName,
    string? ProfileUrl,
    string? SupportExternalId,
    string? TierName,
    RelationshipStatus Status,
    BillingCadence BillingCadence,
    decimal? Amount,
    string? Currency,
    DateTimeOffset StartedAt,
    DateTimeOffset? LastChargeAt,
    DateTimeOffset? NextChargeAt,
    DateTimeOffset? CancelledAt,
    string? ContextJson);

public sealed record RevenueSummaryQuery(
    Guid? MonitoredChannelId,
    DateTimeOffset? Since,
    DateTimeOffset? Until,
    ProviderKind? Provider,
    Guid? StreamSessionId,
    string? CampaignExternalId,
    string? Currency);

public sealed record RevenueSummaryDto(
    DateTimeOffset? Since,
    DateTimeOffset? Until,
    IReadOnlyList<RevenueCurrencySummaryDto> Currencies);

public sealed record RevenueCurrencySummaryDto(
    string Currency,
    decimal Gross,
    decimal KnownNet,
    decimal EstimatedNet,
    decimal PlatformFees,
    decimal ProcessorFees,
    decimal PayoutFees,
    int PositiveCount,
    int NegativeCount,
    int PendingCount,
    int SettledCount,
    int RefundedOrReversedCount);

public sealed record RevenueRankingEntryDto(
    string SupporterKey,
    string DisplayName,
    string Currency,
    decimal Total,
    int Count);

public sealed record ForecastSummaryDto(
    DateTimeOffset From,
    DateTimeOffset Until,
    IReadOnlyList<ForecastCurrencySummaryDto> Currencies);

public sealed record ForecastCurrencySummaryDto(
    string Currency,
    decimal EstimatedGross,
    int ActiveSupportCount);

public sealed record RevenueProviderStatusDto(
    ProviderKind Provider,
    bool Enabled,
    string Status,
    DateTimeOffset? LastSyncedAt,
    string? LastError);

public sealed record ProviderSyncResult(
    ProviderKind Provider,
    bool Success,
    int TipsProcessed,
    int PatronsProcessed,
    string? Error = null);

public sealed record BrowserLoginResultDto(
    ProviderKind Provider,
    string Status,
    string Message,
    string? StorageStatePath);

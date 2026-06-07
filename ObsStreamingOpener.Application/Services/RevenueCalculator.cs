using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class RevenueCalculator(ISupportTransactionStore supportStore) : IRevenueCalculator
{
    public async Task<RevenueSummaryDto> GetSummaryAsync(RevenueSummaryQuery query, CancellationToken cancellationToken = default)
    {
        var tips = await supportStore.QueryTipsAsync(query, cancellationToken);
        return new RevenueSummaryDto(query.Since, query.Until, tips
            .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(ToCurrencySummary)
            .OrderBy(x => x.Currency)
            .ToList());
    }

    public async Task<IReadOnlyList<RevenueRankingEntryDto>> GetRankingAsync(RevenueSummaryQuery query, CancellationToken cancellationToken = default)
    {
        var tips = await supportStore.QueryTipsAsync(query, cancellationToken);
        return tips
            .Where(x => x.Amount > 0)
            .GroupBy(x => new
            {
                Key = !string.IsNullOrWhiteSpace(x.ActorExternalId) ? x.ActorExternalId! : x.ActorName ?? "unknown",
                Display = x.ActorName ?? x.ActorExternalId ?? "Unknown supporter",
                x.Currency
            })
            .Select(x => new RevenueRankingEntryDto(x.Key.Key, x.Key.Display, x.Key.Currency, x.Sum(t => t.Amount), x.Count()))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.DisplayName)
            .ToList();
    }

    private static RevenueCurrencySummaryDto ToCurrencySummary(IGrouping<string, Tip> group)
        => new(
            group.Key,
            group.Sum(x => x.GrossAmount ?? x.Amount),
            group.Sum(x => x.KnownNetAmount ?? 0),
            group.Sum(x => x.EstimatedNetAmount ?? x.KnownNetAmount ?? x.Amount),
            group.Sum(x => x.PlatformFee ?? 0),
            group.Sum(x => x.ProcessorFee ?? 0),
            group.Sum(x => x.PayoutFee ?? 0),
            group.Count(x => x.Amount > 0),
            group.Count(x => x.Amount < 0),
            group.Count(x => x.Status == TipStatus.Pending),
            group.Count(x => x.Status == TipStatus.Settled),
            group.Count(x => x.Status is TipStatus.Refunded or TipStatus.Reversed));
}

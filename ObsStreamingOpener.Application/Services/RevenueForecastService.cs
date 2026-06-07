using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class RevenueForecastService(ISupportTransactionStore supportStore, IClock clock) : IRevenueForecastService
{
    public async Task<ForecastSummaryDto> GetForecastAsync(Guid? monitoredChannelId, int days, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 366);
        var from = clock.UtcNow;
        var until = from.AddDays(days);
        var relationships = await supportStore.QueryPaidRelationshipsAsync(monitoredChannelId, until, cancellationToken);
        var projected = relationships
            .Where(x => x.Status == RelationshipStatus.Active
                && x.Amount.HasValue
                && !string.IsNullOrWhiteSpace(x.Currency)
                && x.NextChargeAt.HasValue
                && x.NextChargeAt.Value >= from
                && x.NextChargeAt.Value < until)
            .Select(x => new { Amount = ProjectAmount(x.Amount!.Value, x.NextChargeAt!.Value, until, x.BillingCadence), Currency = x.Currency!, Id = x.Id })
            .ToList();

        return new ForecastSummaryDto(from, until, projected
            .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ForecastCurrencySummaryDto(x.Key, x.Sum(v => v.Amount), x.Select(v => v.Id).Distinct().Count()))
            .OrderBy(x => x.Currency)
            .ToList());
    }

    private static decimal ProjectAmount(decimal amount, DateTimeOffset nextChargeAt, DateTimeOffset until, BillingCadence cadence)
    {
        var total = 0m;
        var current = nextChargeAt;
        while (current < until)
        {
            total += amount;
            current = cadence switch
            {
                BillingCadence.Monthly => current.AddMonths(1),
                BillingCadence.Quarterly => current.AddMonths(3),
                BillingCadence.Yearly => current.AddYears(1),
                _ => until
            };
        }

        return total;
    }
}

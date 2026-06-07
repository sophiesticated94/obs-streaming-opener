using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IRevenueCalculator
{
    Task<RevenueSummaryDto> GetSummaryAsync(RevenueSummaryQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueRankingEntryDto>> GetRankingAsync(RevenueSummaryQuery query, CancellationToken cancellationToken = default);
}

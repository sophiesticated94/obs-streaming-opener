using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IRevenueSynchronizer
{
    Task<IReadOnlyList<ProviderSyncResult>> SyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueProviderStatusDto>> GetProviderStatusesAsync(CancellationToken cancellationToken = default);
}

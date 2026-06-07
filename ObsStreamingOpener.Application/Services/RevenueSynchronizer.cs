using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Services;

public sealed class RevenueSynchronizer(
    IEnumerable<ISupportProviderAdapter> adapters,
    ISupportIngestionService ingestionService,
    ILogger<RevenueSynchronizer> logger) : IRevenueSynchronizer
{
    private readonly List<RevenueProviderStatusDto> _statuses = [];

    public async Task<IReadOnlyList<ProviderSyncResult>> SyncAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ProviderSyncResult>();
        foreach (var adapter in adapters.Where(x => x.Enabled))
        {
            var tips = 0;
            var patrons = 0;
            try
            {
                await foreach (var tip in adapter.GetTipsAsync(cancellationToken))
                {
                    await ingestionService.IngestTipAsync(tip, cancellationToken);
                    tips++;
                }

                await foreach (var patron in adapter.GetPatronsAsync(cancellationToken))
                {
                    await ingestionService.IngestPatronAsync(patron, cancellationToken);
                    patrons++;
                }

                results.Add(new ProviderSyncResult(adapter.Provider, Success: true, tips, patrons));
                SetStatus(adapter.Provider, "Ok", null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Revenue provider {Provider} sync failed.", adapter.Provider);
                results.Add(new ProviderSyncResult(adapter.Provider, Success: false, tips, patrons, ex.Message));
                SetStatus(adapter.Provider, "Error", ex.Message);
            }
        }

        return results;
    }

    public Task<IReadOnlyList<RevenueProviderStatusDto>> GetProviderStatusesAsync(CancellationToken cancellationToken = default)
    {
        var current = adapters
            .Select(x => _statuses.FirstOrDefault(s => s.Provider == x.Provider)
                ?? new RevenueProviderStatusDto(x.Provider, x.Enabled, x.Enabled ? "NotSynced" : "Disabled", null, null))
            .ToList();
        return Task.FromResult<IReadOnlyList<RevenueProviderStatusDto>>(current);
    }

    private void SetStatus(ObsStreamingOpener.Domain.ProviderKind provider, string status, string? error)
    {
        _statuses.RemoveAll(x => x.Provider == provider);
        _statuses.Add(new RevenueProviderStatusDto(provider, Enabled: true, status, DateTimeOffset.UtcNow, error));
    }
}

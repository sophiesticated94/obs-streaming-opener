using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface ISupportProviderAdapter
{
    ProviderKind Provider { get; }

    bool Enabled { get; }

    IAsyncEnumerable<ProviderTipRecord> GetTipsAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<ProviderPatronRecord> GetPatronsAsync(CancellationToken cancellationToken = default);
}

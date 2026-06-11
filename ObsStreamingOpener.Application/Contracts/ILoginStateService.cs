using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface ILoginStateService
{
    Task<bool> HasStateAsync(ProviderKind provider, CancellationToken cancellationToken = default);

    Task<string> GetStatePathAsync(ProviderKind provider, CancellationToken cancellationToken = default);

    Task SaveStateAsync(ProviderKind provider, string storageStateJson, CancellationToken cancellationToken = default);

    Task DeleteTemporaryStateAsync(string statePath, CancellationToken cancellationToken = default);

    Task<BrowserLoginResultDto> ClearStateAsync(ProviderKind provider, CancellationToken cancellationToken = default);
}

using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderBrowserSessionStore
{
    Task<ProviderBrowserSessionDto?> GetBrowserSessionAsync(ProviderKind provider, CancellationToken cancellationToken = default);

    Task<ProviderBrowserSessionDto> UpsertBrowserSessionAsync(
        ProviderKind provider,
        string encryptedStorageStateJson,
        string status,
        CancellationToken cancellationToken = default);

    Task<ProviderBrowserSessionDto> MarkBrowserSessionStatusAsync(
        ProviderKind provider,
        string status,
        string? encryptedStorageStateJson = null,
        CancellationToken cancellationToken = default);

    Task<bool> ClearBrowserSessionAsync(ProviderKind provider, CancellationToken cancellationToken = default);
}

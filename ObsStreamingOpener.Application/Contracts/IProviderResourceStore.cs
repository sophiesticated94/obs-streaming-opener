using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderResourceStore
{
    Task<ProviderResourceDto> UpsertResourceAsync(ProviderResourceUpsert resource, CancellationToken cancellationToken = default);

    Task<ProviderResourceDto?> GetResourceAsync(Guid monitoredChannelId, Guid providerResourceId, CancellationToken cancellationToken = default);

    Task<ProviderResourceDto?> GetResourceByExternalIdAsync(Guid monitoredChannelId, ProviderKind provider, string externalResourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderResourceDto>> GetRecentResourcesAsync(
        Guid monitoredChannelId,
        ProviderResourceKind? resourceKind,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderResourceDto>> GetUpcomingResourcesAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default);
}

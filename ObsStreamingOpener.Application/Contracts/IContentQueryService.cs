using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IContentQueryService
{
    Task<IReadOnlyList<ProviderResourceDto>> GetRecentContentAsync(Guid monitoredChannelId, ProviderResourceKind? kind, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderResourceDto>> GetUpcomingContentAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default);

    Task<ProviderResourceDto?> GetContentAsync(Guid monitoredChannelId, Guid providerResourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentEventDto>> GetRecentCommentsAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default);

    Task<ChannelContentOverviewDto> GetYouTubeOverviewAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);
}

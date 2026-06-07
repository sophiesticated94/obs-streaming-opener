using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class ContentQueryService(
    IChannelStore channelStore,
    IProviderResourceStore resourceStore,
    IEventStore eventStore,
    IClock clock) : IContentQueryService
{
    public async Task<IReadOnlyList<ProviderResourceDto>> GetRecentContentAsync(Guid monitoredChannelId, ProviderResourceKind? kind, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(monitoredChannelId, cancellationToken);
        return await resourceStore.GetRecentResourcesAsync(monitoredChannelId, kind, limit, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderResourceDto>> GetUpcomingContentAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(monitoredChannelId, cancellationToken);
        return await resourceStore.GetUpcomingResourcesAsync(monitoredChannelId, limit, cancellationToken);
    }

    public async Task<ProviderResourceDto?> GetContentAsync(Guid monitoredChannelId, Guid providerResourceId, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(monitoredChannelId, cancellationToken);
        return await resourceStore.GetResourceAsync(monitoredChannelId, providerResourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecentEventDto>> GetRecentCommentsAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(monitoredChannelId, cancellationToken);
        return await eventStore.GetRecentEventsAsync(monitoredChannelId, ProviderKind.YouTube, StreamEventType.CommentCreated, limit, cancellationToken: cancellationToken);
    }

    public async Task<ChannelContentOverviewDto> GetYouTubeOverviewAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        await EnsureChannelExistsAsync(monitoredChannelId, cancellationToken);
        var recent = await resourceStore.GetRecentResourcesAsync(monitoredChannelId, null, 10, cancellationToken);
        var upcoming = await resourceStore.GetUpcomingResourcesAsync(monitoredChannelId, 5, cancellationToken);
        var comments = await eventStore.GetRecentEventsAsync(monitoredChannelId, ProviderKind.YouTube, StreamEventType.CommentCreated, 10, cancellationToken: cancellationToken);

        return new ChannelContentOverviewDto(
            monitoredChannelId,
            recent.FirstOrDefault(x => x.ResourceKind == ProviderResourceKind.Video),
            upcoming.FirstOrDefault(),
            recent,
            comments,
            clock.UtcNow);
    }

    private async Task EnsureChannelExistsAsync(Guid monitoredChannelId, CancellationToken cancellationToken)
    {
        if (await channelStore.GetChannelAsync(monitoredChannelId, cancellationToken) is null)
        {
            throw new InvalidOperationException("Monitored channel was not found.");
        }
    }
}

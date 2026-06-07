using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Infrastructure.YouTube;

public sealed class YouTubeStreamProviderDataProvider(
    IStreamSessionStore streamSessionStore,
    IYouTubeCredentialResolver credentialResolver,
    IYouTubeApiClient youtubeApiClient,
    IClock clock) : IStreamProviderDataProvider
{
    public async Task<StreamProviderDataSnapshot?> GetCurrentStreamDataAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var session = await streamSessionStore.GetCurrentSessionAsync(monitoredChannelId, cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.ExternalStreamId))
        {
            return null;
        }

        var credential = await credentialResolver.ResolveForChannelAsync(monitoredChannelId, cancellationToken);
        var stats = await youtubeApiClient.GetViewerStatsAsync(session.ExternalStreamId, credential?.AccessToken, cancellationToken);
        return new StreamProviderDataSnapshot(
            monitoredChannelId,
            ProviderKind.YouTube,
            session.Id,
            session.ExternalStreamId,
            session.ExternalLiveChatId,
            stats?.ConcurrentViewers,
            stats?.Likes,
            clock.UtcNow);
    }
}

using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IYouTubeApiClient
{
    Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, string? accessToken = null, CancellationToken cancellationToken = default);

    Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, string? accessToken = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<YouTubeChannelSummary>> GetMyChannelSummariesAsync(string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<YouTubePage<YouTubeActivityEvent>> GetChannelActivitiesAsync(string channelId, string? pageToken, string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<YouTubePage<YouTubeContentItem>> GetUploadsAsync(string uploadsPlaylistId, string? pageToken, string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<IReadOnlyList<YouTubeContentItem>> GetVideosAsync(IReadOnlyList<string> videoIds, string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<YouTubePage<YouTubeCommentEvent>> GetCommentThreadsAsync(string videoId, string? pageToken, string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<YouTubePage<YouTubeVisibleSubscriber>> GetVisibleSubscribersAsync(string? pageToken, string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<YouTubePage<YouTubeSuperChatEvent>> GetSuperChatEventsAsync(string? pageToken, string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<IReadOnlyList<YouTubeContentItem>> GetLiveBroadcastsAsync(string broadcastStatus, string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<IReadOnlyList<YouTubeContentItem>> GetLiveStreamsAsync(string accessToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

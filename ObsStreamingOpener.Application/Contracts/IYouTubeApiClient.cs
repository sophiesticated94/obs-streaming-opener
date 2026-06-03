using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IYouTubeApiClient
{
    Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, CancellationToken cancellationToken = default);

    Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, CancellationToken cancellationToken = default);
}

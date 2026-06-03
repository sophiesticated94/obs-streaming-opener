using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Infrastructure.Options;

namespace ObsStreamingOpener.Infrastructure.YouTube;

public sealed class YouTubeApiClient(
    HttpClient httpClient,
    IOptions<YouTubeOptions> options,
    ILogger<YouTubeApiClient> logger) : IYouTubeApiClient
{
    private readonly YouTubeOptions _options = options.Value;

    public async Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogDebug("YouTube API key is not configured; skipping viewer stats poll.");
            return null;
        }

        var url = $"videos?part=liveStreamingDetails,statistics&id={Uri.EscapeDataString(videoId)}&key={Uri.EscapeDataString(_options.ApiKey)}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("YouTube viewer stats request failed with {StatusCode}: {Payload}", response.StatusCode, payload);
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        var item = document.RootElement.GetProperty("items").EnumerateArray().FirstOrDefault();
        if (item.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        long? concurrentViewers = null;
        if (item.TryGetProperty("liveStreamingDetails", out var liveDetails)
            && liveDetails.TryGetProperty("concurrentViewers", out var concurrentElement)
            && long.TryParse(concurrentElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedViewers))
        {
            concurrentViewers = parsedViewers;
        }

        long? likes = null;
        if (item.TryGetProperty("statistics", out var statistics)
            && statistics.TryGetProperty("likeCount", out var likesElement)
            && long.TryParse(likesElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLikes))
        {
            likes = parsedLikes;
        }

        return new YouTubeViewerStats(videoId, concurrentViewers, likes, payload);
    }

    public async Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogDebug("YouTube API key is not configured; skipping live chat poll.");
            return new YouTubeChatPollResult([], pageToken, TimeSpan.FromSeconds(10));
        }

        var url = $"liveChat/messages?part=snippet,authorDetails&liveChatId={Uri.EscapeDataString(liveChatId)}&key={Uri.EscapeDataString(_options.ApiKey)}";
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }

        using var response = await httpClient.GetAsync(url, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("YouTube live chat request failed with {StatusCode}: {Payload}", response.StatusCode, payload);
            return new YouTubeChatPollResult([], pageToken, TimeSpan.FromSeconds(10));
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var nextPageToken = root.TryGetProperty("nextPageToken", out var nextPageTokenElement)
            ? nextPageTokenElement.GetString()
            : pageToken;
        var pollingInterval = root.TryGetProperty("pollingIntervalMillis", out var intervalElement)
            && intervalElement.TryGetInt32(out var intervalMillis)
                ? TimeSpan.FromMilliseconds(Math.Max(intervalMillis, 1000))
                : TimeSpan.FromSeconds(10);

        var messages = new List<YouTubeChatMessage>();
        if (root.TryGetProperty("items", out var itemsElement))
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var snippet = item.GetProperty("snippet");
                var authorDetails = item.GetProperty("authorDetails");
                var publishedAt = snippet.TryGetProperty("publishedAt", out var publishedAtElement)
                    && publishedAtElement.TryGetDateTimeOffset(out var parsedDate)
                        ? parsedDate
                        : DateTimeOffset.UtcNow;

                messages.Add(new YouTubeChatMessage(
                    id,
                    authorDetails.TryGetProperty("displayName", out var authorName) ? authorName.GetString() : null,
                    authorDetails.TryGetProperty("channelId", out var channelId) ? channelId.GetString() : null,
                    snippet.TryGetProperty("displayMessage", out var message) ? message.GetString() : null,
                    publishedAt,
                    item.GetRawText()));
            }
        }

        return new YouTubeChatPollResult(messages, nextPageToken, pollingInterval);
    }
}

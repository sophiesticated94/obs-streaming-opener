using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Http;
using ObsStreamingOpener.Infrastructure.Options;

namespace ObsStreamingOpener.Infrastructure.YouTube;

public sealed class YouTubeApiClient(
    IExternalHttpClient httpClient,
    IOptions<YouTubeOptions> options,
    ILogger<YouTubeApiClient> logger) : IYouTubeApiClient
{
    private const string ServiceName = "YouTube";
    private readonly YouTubeOptions _options = options.Value;

    public async Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) && string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogDebug("YouTube OAuth token/API key is not configured; skipping viewer stats poll.");
            return null;
        }

        var url = CreateYouTubeUri($"videos?part=liveStreamingDetails,statistics&id={Uri.EscapeDataString(videoId)}");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            url = new Uri($"{url}&key={Uri.EscapeDataString(_options.ApiKey!)}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeVideosListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        var item = response.Body.Items.FirstOrDefault();
        if (item is null)
        {
            return null;
        }

        return new YouTubeViewerStats(
            videoId,
            ParseLong(item.LiveStreamingDetails?.ConcurrentViewers),
            ParseLong(item.Statistics?.LikeCount),
            response.RawBody);
    }

    public async Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken) && string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogDebug("YouTube OAuth token/API key is not configured; skipping live chat poll.");
            return new YouTubeChatPollResult([], pageToken, TimeSpan.FromSeconds(10));
        }

        var url = CreateYouTubeUri($"liveChat/messages?part=snippet,authorDetails&liveChatId={Uri.EscapeDataString(liveChatId)}");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            url = new Uri($"{url}&key={Uri.EscapeDataString(_options.ApiKey!)}");
        }

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            url = new Uri($"{url}&pageToken={Uri.EscapeDataString(pageToken)}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeLiveChatMessagesResponse>(request, ServiceName, cancellationToken: cancellationToken);
        var nextPageToken = response.Body.NextPageToken ?? pageToken;
        var pollingInterval = response.Body.PollingIntervalMillis.HasValue
            ? TimeSpan.FromMilliseconds(Math.Max(response.Body.PollingIntervalMillis.Value, 1000))
            : TimeSpan.FromSeconds(10);

        var messages = new List<YouTubeChatMessage>();
        foreach (var item in response.Body.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            messages.Add(new YouTubeChatMessage(
                item.Id,
                item.Snippet?.Type,
                item.AuthorDetails?.DisplayName,
                item.AuthorDetails?.ChannelId,
                item.AuthorDetails?.ProfileImageUrl,
                item.Snippet?.SuperChatDetails?.UserComment
                    ?? item.Snippet?.SuperStickerDetails?.SuperStickerMetadata?.AltText
                    ?? item.Snippet?.MemberMilestoneChatDetails?.UserComment
                    ?? item.Snippet?.DisplayMessage,
                item.Snippet?.PublishedAt ?? DateTimeOffset.UtcNow,
                response.RawBody,
                item.AuthorDetails?.IsChatOwner ?? false,
                item.AuthorDetails?.IsChatModerator ?? false,
                item.AuthorDetails?.IsVerified ?? false,
                item.AuthorDetails?.IsChatSponsor ?? false,
                MicrosToAmount(item.Snippet?.SuperChatDetails?.AmountMicros ?? item.Snippet?.SuperStickerDetails?.AmountMicros),
                item.Snippet?.SuperChatDetails?.Currency ?? item.Snippet?.SuperStickerDetails?.Currency,
                item.Snippet?.SuperChatDetails?.AmountDisplayString ?? item.Snippet?.SuperStickerDetails?.AmountDisplayString,
                item.Snippet?.SuperChatDetails?.UserComment,
                item.Snippet?.SuperChatDetails?.Tier ?? item.Snippet?.SuperStickerDetails?.Tier,
                item.Snippet?.SuperStickerDetails?.SuperStickerMetadata?.StickerId,
                item.Snippet?.SuperStickerDetails?.SuperStickerMetadata?.AltText,
                item.Snippet?.NewSponsorDetails?.MemberLevelName
                    ?? item.Snippet?.MembershipGiftingDetails?.GiftMembershipsLevelName
                    ?? item.Snippet?.GiftMembershipReceivedDetails?.MemberLevelName
                    ?? item.Snippet?.MemberMilestoneChatDetails?.MemberLevelName,
                item.Snippet?.NewSponsorDetails?.IsUpgrade,
                item.Snippet?.MembershipGiftingDetails?.GiftMembershipsCount,
                item.Snippet?.GiftMembershipReceivedDetails?.GifterChannelId,
                item.Snippet?.GiftMembershipReceivedDetails?.AssociatedMembershipGiftingMessageId,
                item.Snippet?.MemberMilestoneChatDetails?.MemberMonth));
        }

        return new YouTubeChatPollResult(messages, nextPageToken, pollingInterval);
    }

    public async Task<IReadOnlyList<YouTubeChannelSummary>> GetMyChannelSummariesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CreateYouTubeUri("channels?part=id,snippet,statistics,contentDetails,brandingSettings,status&mine=true"));
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeChannelsListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return response.Body.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => new YouTubeChannelSummary(
                x.Id!,
                x.Snippet?.Title ?? x.Id!,
                $"https://www.youtube.com/channel/{x.Id}",
                ParseLong(x.Statistics?.SubscriberCount),
                ParseLong(x.Statistics?.ViewCount),
                ParseLong(x.Statistics?.VideoCount),
                x.ContentDetails?.RelatedPlaylists?.Uploads,
                x.Status?.PrivacyStatus,
                response.RawBody))
            .ToList();
    }

    public async Task<YouTubePage<YouTubeActivityEvent>> GetChannelActivitiesAsync(string channelId, string? pageToken, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = CreateYouTubeUri($"activities?part=snippet,contentDetails&channelId={Uri.EscapeDataString(channelId)}&maxResults=25");
        url = AddPageToken(url, pageToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeActivitiesListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return new YouTubePage<YouTubeActivityEvent>(
            response.Body.Items
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => new YouTubeActivityEvent(
                    x.Id!,
                    x.Snippet?.Title,
                    x.Snippet?.Description,
                    x.ContentDetails?.Upload?.VideoId ?? x.ContentDetails?.PlaylistItem?.ResourceId?.VideoId,
                    x.Snippet?.Type,
                    x.Snippet?.PublishedAt,
                    response.RawBody))
                .ToList(),
            response.Body.NextPageToken,
            response.RawBody);
    }

    public async Task<YouTubePage<YouTubeContentItem>> GetUploadsAsync(string uploadsPlaylistId, string? pageToken, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = CreateYouTubeUri($"playlistItems?part=snippet,contentDetails,status&playlistId={Uri.EscapeDataString(uploadsPlaylistId)}&maxResults=25");
        url = AddPageToken(url, pageToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubePlaylistItemsListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return new YouTubePage<YouTubeContentItem>(
            response.Body.Items
                .Select(x => ToContentItem(
                    x.ContentDetails?.VideoId ?? x.Snippet?.ResourceId?.VideoId ?? x.Id,
                    ProviderResourceKind.Video,
                    x.Snippet?.Title,
                    x.Snippet?.Description,
                    BestThumbnail(x.Snippet?.Thumbnails),
                    x.Status?.PrivacyStatus,
                    x.ContentDetails?.VideoPublishedAt ?? x.Snippet?.PublishedAt,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    response.RawBody,
                    null,
                    null,
                    null))
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList(),
            response.Body.NextPageToken,
            response.RawBody);
    }

    public async Task<IReadOnlyList<YouTubeContentItem>> GetVideosAsync(IReadOnlyList<string> videoIds, string accessToken, CancellationToken cancellationToken = default)
    {
        var ids = videoIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(50).ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, CreateYouTubeUri($"videos?part=id,snippet,statistics,contentDetails,liveStreamingDetails,status&id={Uri.EscapeDataString(string.Join(',', ids))}"));
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeVideosListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return response.Body.Items
            .Select(x => ToContentItem(
                x.Id,
                ProviderResourceKind.Video,
                x.Snippet?.Title,
                x.Snippet?.Description,
                BestThumbnail(x.Snippet?.Thumbnails),
                x.Status?.PrivacyStatus,
                x.Snippet?.PublishedAt,
                x.LiveStreamingDetails?.ScheduledStartTime,
                x.LiveStreamingDetails?.ActualStartTime,
                x.LiveStreamingDetails?.ActualEndTime,
                ParseDurationSeconds(x.ContentDetails?.Duration),
                ParseLong(x.Statistics?.ViewCount),
                ParseLong(x.Statistics?.LikeCount),
                ParseLong(x.Statistics?.CommentCount),
                response.RawBody,
                x.Snippet?.ChannelId,
                x.LiveStreamingDetails?.ActiveLiveChatId,
                null))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }

    public async Task<YouTubePage<YouTubeCommentEvent>> GetCommentThreadsAsync(string videoId, string? pageToken, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = CreateYouTubeUri($"commentThreads?part=snippet&videoId={Uri.EscapeDataString(videoId)}&maxResults=25&order=time");
        url = AddPageToken(url, pageToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeCommentThreadsListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return new YouTubePage<YouTubeCommentEvent>(
            response.Body.Items
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .Select(x =>
                {
                    var snippet = x.Snippet?.TopLevelComment?.Snippet;
                    return new YouTubeCommentEvent(
                        x.Id!,
                        snippet?.AuthorDisplayName,
                        snippet?.AuthorChannelId?.Value,
                        snippet?.TextDisplay,
                        snippet?.LikeCount,
                        snippet?.PublishedAt,
                        response.RawBody);
                })
                .ToList(),
            response.Body.NextPageToken,
            response.RawBody);
    }

    public async Task<YouTubePage<YouTubeVisibleSubscriber>> GetVisibleSubscribersAsync(string? pageToken, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = CreateYouTubeUri("subscriptions?part=snippet&mySubscribers=true&maxResults=25");
        url = AddPageToken(url, pageToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeSubscriptionsListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return new YouTubePage<YouTubeVisibleSubscriber>(
            response.Body.Items
                .Select(x => new YouTubeVisibleSubscriber(
                    x.Snippet?.ResourceId?.ChannelId ?? x.Id ?? string.Empty,
                    x.Snippet?.Title,
                    x.Snippet?.Thumbnails?.Default?.Url,
                    x.Snippet?.PublishedAt,
                    response.RawBody))
                .Where(x => !string.IsNullOrWhiteSpace(x.ChannelId))
                .ToList(),
            response.Body.NextPageToken,
            response.RawBody);
    }

    public async Task<YouTubePage<YouTubeSuperChatEvent>> GetSuperChatEventsAsync(string? pageToken, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = CreateYouTubeUri("superChatEvents?part=id,snippet&maxResults=50");
        url = AddPageToken(url, pageToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeSuperChatEventsListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return new YouTubePage<YouTubeSuperChatEvent>(
            response.Body.Items
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .Select(x => new YouTubeSuperChatEvent(
                    x.Id!,
                    x.Snippet?.ChannelId,
                    x.Snippet?.SupporterDetails?.ChannelId,
                    x.Snippet?.SupporterDetails?.DisplayName,
                    x.Snippet?.SupporterDetails?.ProfileImageUrl,
                    x.Snippet?.CommentText,
                    MicrosToAmount(x.Snippet?.AmountMicros),
                    x.Snippet?.Currency,
                    x.Snippet?.DisplayString,
                    x.Snippet?.MessageType,
                    x.Snippet?.CreatedAt,
                    response.RawBody))
                .ToList(),
            response.Body.NextPageToken,
            response.RawBody);
    }

    public async Task<IReadOnlyList<YouTubeContentItem>> GetLiveBroadcastsAsync(string broadcastStatus, string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CreateYouTubeUri($"liveBroadcasts?part=id,snippet,status,contentDetails&broadcastStatus={Uri.EscapeDataString(broadcastStatus)}&broadcastType=all&maxResults=25"));
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeLiveBroadcastsListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return response.Body.Items
            .Select(x => ToContentItem(
                x.Id,
                ProviderResourceKind.LiveBroadcast,
                x.Snippet?.Title,
                x.Snippet?.Description,
                BestThumbnail(x.Snippet?.Thumbnails),
                x.Status?.LifeCycleStatus ?? broadcastStatus,
                x.Snippet?.PublishedAt,
                x.Snippet?.ScheduledStartTime,
                x.Snippet?.ActualStartTime,
                x.Snippet?.ActualEndTime,
                null,
                null,
                null,
                null,
                response.RawBody,
                x.Snippet?.ChannelId,
                x.Snippet?.LiveChatId,
                x.ContentDetails?.BoundStreamId))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }

    public async Task<IReadOnlyList<YouTubeContentItem>> GetLiveStreamsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CreateYouTubeUri("liveStreams?part=id,snippet,status,cdn&mine=true&maxResults=25"));
        AddBearerToken(request, accessToken);
        var response = await httpClient.SendWithBodyAsync<YouTubeLiveStreamsListResponse>(request, ServiceName, cancellationToken: cancellationToken);

        return response.Body.Items
            .Select(x => ToContentItem(
                x.Id,
                ProviderResourceKind.LiveStream,
                x.Snippet?.Title,
                x.Snippet?.Description,
                BestThumbnail(x.Snippet?.Thumbnails),
                x.Status?.StreamStatus,
                x.Snippet?.PublishedAt,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                response.RawBody,
                x.Snippet?.ChannelId,
                null,
                null))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }

    private static void AddBearerToken(HttpRequestMessage request, string? accessToken)
    {
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    private Uri CreateYouTubeUri(string relativeUrl)
        => new(new Uri(_options.BaseUrl), relativeUrl);

    private static Uri AddPageToken(Uri url, string? pageToken)
        => string.IsNullOrWhiteSpace(pageToken) ? url : new Uri($"{url}&pageToken={Uri.EscapeDataString(pageToken)}");

    private static long? ParseLong(string? value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static decimal? MicrosToAmount(ulong? amountMicros)
        => amountMicros.HasValue ? amountMicros.Value / 1_000_000m : null;

    private static int? ParseDurationSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
        {
            return null;
        }

        try
        {
            return (int)Math.Round(XmlConvert.ToTimeSpan(duration).TotalSeconds);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? BestThumbnail(YouTubeThumbnailsResponse? thumbnails)
        => thumbnails?.Maxres?.Url
            ?? thumbnails?.Standard?.Url
            ?? thumbnails?.High?.Url
            ?? thumbnails?.Medium?.Url
            ?? thumbnails?.Default?.Url;

    private static YouTubeContentItem? ToContentItem(
        string? id,
        ProviderResourceKind kind,
        string? title,
        string? description,
        string? thumbnailUrl,
        string? status,
        DateTimeOffset? publishedAt,
        DateTimeOffset? scheduledStartAt,
        DateTimeOffset? actualStartAt,
        DateTimeOffset? actualEndAt,
        int? durationSeconds,
        long? viewCount,
        long? likeCount,
        long? commentCount,
        string rawPayloadJson,
        string? channelId = null,
        string? liveChatId = null,
        string? boundStreamId = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new YouTubeContentItem(
            id,
            kind,
            title,
            description,
            kind is ProviderResourceKind.Video or ProviderResourceKind.LiveBroadcast ? $"https://www.youtube.com/watch?v={id}" : null,
            thumbnailUrl,
            status,
            publishedAt,
            scheduledStartAt,
            actualStartAt,
            actualEndAt,
            durationSeconds,
            viewCount,
            likeCount,
            commentCount,
            rawPayloadJson,
            channelId,
            liveChatId,
            boundStreamId);
    }

    private sealed record YouTubeVideosListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeVideoItemResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeVideoItemResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeSnippetResponse? Snippet,
        [property: JsonPropertyName("status")] YouTubePrivacyStatusResponse? Status,
        [property: JsonPropertyName("liveStreamingDetails")] YouTubeLiveStreamingDetailsResponse? LiveStreamingDetails,
        [property: JsonPropertyName("contentDetails")] YouTubeVideoContentDetailsResponse? ContentDetails,
        [property: JsonPropertyName("statistics")] YouTubeVideoStatisticsResponse? Statistics);

    private sealed record YouTubeLiveStreamingDetailsResponse(
        [property: JsonPropertyName("concurrentViewers")] string? ConcurrentViewers,
        [property: JsonPropertyName("scheduledStartTime")] DateTimeOffset? ScheduledStartTime,
        [property: JsonPropertyName("actualStartTime")] DateTimeOffset? ActualStartTime,
        [property: JsonPropertyName("actualEndTime")] DateTimeOffset? ActualEndTime,
        [property: JsonPropertyName("activeLiveChatId")] string? ActiveLiveChatId);

    private sealed record YouTubeVideoStatisticsResponse(
        [property: JsonPropertyName("viewCount")] string? ViewCount,
        [property: JsonPropertyName("likeCount")] string? LikeCount,
        [property: JsonPropertyName("commentCount")] string? CommentCount);

    private sealed record YouTubeVideoContentDetailsResponse(
        [property: JsonPropertyName("duration")] string? Duration);

    private sealed record YouTubeLiveChatMessagesResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("pollingIntervalMillis")]
        public int? PollingIntervalMillis { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeLiveChatMessageItemResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeLiveChatMessageItemResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeLiveChatSnippetResponse? Snippet,
        [property: JsonPropertyName("authorDetails")] YouTubeLiveChatAuthorDetailsResponse? AuthorDetails);

    private sealed record YouTubeLiveChatSnippetResponse(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("displayMessage")] string? DisplayMessage,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("superChatDetails")] YouTubeLiveChatSuperChatDetailsResponse? SuperChatDetails,
        [property: JsonPropertyName("superStickerDetails")] YouTubeLiveChatSuperStickerDetailsResponse? SuperStickerDetails,
        [property: JsonPropertyName("newSponsorDetails")] YouTubeLiveChatNewSponsorDetailsResponse? NewSponsorDetails,
        [property: JsonPropertyName("membershipGiftingDetails")] YouTubeLiveChatMembershipGiftingDetailsResponse? MembershipGiftingDetails,
        [property: JsonPropertyName("giftMembershipReceivedDetails")] YouTubeLiveChatGiftMembershipReceivedDetailsResponse? GiftMembershipReceivedDetails,
        [property: JsonPropertyName("memberMilestoneChatDetails")] YouTubeLiveChatMemberMilestoneChatDetailsResponse? MemberMilestoneChatDetails);

    private sealed record YouTubeLiveChatSuperChatDetailsResponse(
        [property: JsonPropertyName("amountMicros")] ulong? AmountMicros,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("amountDisplayString")] string? AmountDisplayString,
        [property: JsonPropertyName("userComment")] string? UserComment,
        [property: JsonPropertyName("tier")] long? Tier);

    private sealed record YouTubeLiveChatSuperStickerDetailsResponse(
        [property: JsonPropertyName("amountMicros")] ulong? AmountMicros,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("amountDisplayString")] string? AmountDisplayString,
        [property: JsonPropertyName("tier")] long? Tier,
        [property: JsonPropertyName("superStickerMetadata")] YouTubeLiveChatSuperStickerMetadataResponse? SuperStickerMetadata);

    private sealed record YouTubeLiveChatSuperStickerMetadataResponse(
        [property: JsonPropertyName("stickerId")] string? StickerId,
        [property: JsonPropertyName("altText")] string? AltText,
        [property: JsonPropertyName("language")] string? Language);

    private sealed record YouTubeLiveChatNewSponsorDetailsResponse(
        [property: JsonPropertyName("memberLevelName")] string? MemberLevelName,
        [property: JsonPropertyName("isUpgrade")] bool? IsUpgrade);

    private sealed record YouTubeLiveChatMembershipGiftingDetailsResponse(
        [property: JsonPropertyName("giftMembershipsCount")] int? GiftMembershipsCount,
        [property: JsonPropertyName("giftMembershipsLevelName")] string? GiftMembershipsLevelName);

    private sealed record YouTubeLiveChatGiftMembershipReceivedDetailsResponse(
        [property: JsonPropertyName("memberLevelName")] string? MemberLevelName,
        [property: JsonPropertyName("gifterChannelId")] string? GifterChannelId,
        [property: JsonPropertyName("associatedMembershipGiftingMessageId")] string? AssociatedMembershipGiftingMessageId);

    private sealed record YouTubeLiveChatMemberMilestoneChatDetailsResponse(
        [property: JsonPropertyName("userComment")] string? UserComment,
        [property: JsonPropertyName("memberMonth")] int? MemberMonth,
        [property: JsonPropertyName("memberLevelName")] string? MemberLevelName);

    private sealed record YouTubeLiveChatAuthorDetailsResponse(
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("channelId")] string? ChannelId,
        [property: JsonPropertyName("profileImageUrl")] string? ProfileImageUrl,
        [property: JsonPropertyName("isChatOwner")] bool? IsChatOwner,
        [property: JsonPropertyName("isChatModerator")] bool? IsChatModerator,
        [property: JsonPropertyName("isVerified")] bool? IsVerified,
        [property: JsonPropertyName("isChatSponsor")] bool? IsChatSponsor);

    private sealed record YouTubeChannelsListResponse
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeChannelItemResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeChannelItemResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeSnippetResponse? Snippet,
        [property: JsonPropertyName("statistics")] YouTubeChannelStatisticsResponse? Statistics,
        [property: JsonPropertyName("contentDetails")] YouTubeChannelContentDetailsResponse? ContentDetails,
        [property: JsonPropertyName("status")] YouTubePrivacyStatusResponse? Status);

    private sealed record YouTubeChannelStatisticsResponse(
        [property: JsonPropertyName("subscriberCount")] string? SubscriberCount,
        [property: JsonPropertyName("viewCount")] string? ViewCount,
        [property: JsonPropertyName("videoCount")] string? VideoCount);

    private sealed record YouTubeChannelContentDetailsResponse(
        [property: JsonPropertyName("relatedPlaylists")] YouTubeRelatedPlaylistsResponse? RelatedPlaylists);

    private sealed record YouTubeRelatedPlaylistsResponse(
        [property: JsonPropertyName("uploads")] string? Uploads);

    private sealed record YouTubeActivitiesListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeActivityItemResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeActivityItemResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeActivitySnippetResponse? Snippet,
        [property: JsonPropertyName("contentDetails")] YouTubeActivityContentDetailsResponse? ContentDetails);

    private sealed record YouTubeActivitySnippetResponse(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt);

    private sealed record YouTubeActivityContentDetailsResponse(
        [property: JsonPropertyName("upload")] YouTubeUploadActivityResponse? Upload,
        [property: JsonPropertyName("playlistItem")] YouTubePlaylistItemActivityResponse? PlaylistItem);

    private sealed record YouTubeUploadActivityResponse([property: JsonPropertyName("videoId")] string? VideoId);

    private sealed record YouTubePlaylistItemActivityResponse([property: JsonPropertyName("resourceId")] YouTubeResourceIdResponse? ResourceId);

    private sealed record YouTubePlaylistItemsListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubePlaylistItemResponse> Items { get; init; } = [];
    }

    private sealed record YouTubePlaylistItemResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubePlaylistSnippetResponse? Snippet,
        [property: JsonPropertyName("contentDetails")] YouTubePlaylistContentDetailsResponse? ContentDetails,
        [property: JsonPropertyName("status")] YouTubePrivacyStatusResponse? Status);

    private sealed record YouTubePlaylistSnippetResponse(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("thumbnails")] YouTubeThumbnailsResponse? Thumbnails,
        [property: JsonPropertyName("resourceId")] YouTubeResourceIdResponse? ResourceId);

    private sealed record YouTubePlaylistContentDetailsResponse(
        [property: JsonPropertyName("videoId")] string? VideoId,
        [property: JsonPropertyName("videoPublishedAt")] DateTimeOffset? VideoPublishedAt);

    private sealed record YouTubeCommentThreadsListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeCommentThreadResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeCommentThreadResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeCommentThreadSnippetResponse? Snippet);

    private sealed record YouTubeCommentThreadSnippetResponse(
        [property: JsonPropertyName("topLevelComment")] YouTubeCommentResponse? TopLevelComment);

    private sealed record YouTubeCommentResponse([property: JsonPropertyName("snippet")] YouTubeCommentSnippetResponse? Snippet);

    private sealed record YouTubeCommentSnippetResponse(
        [property: JsonPropertyName("authorDisplayName")] string? AuthorDisplayName,
        [property: JsonPropertyName("authorChannelId")] YouTubeAuthorChannelIdResponse? AuthorChannelId,
        [property: JsonPropertyName("textDisplay")] string? TextDisplay,
        [property: JsonPropertyName("likeCount")] long? LikeCount,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt);

    private sealed record YouTubeAuthorChannelIdResponse([property: JsonPropertyName("value")] string? Value);

    private sealed record YouTubeSubscriptionsListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeSubscriptionItemResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeSubscriptionItemResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeSubscriptionSnippetResponse? Snippet);

    private sealed record YouTubeSubscriptionSnippetResponse(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("resourceId")] YouTubeResourceIdResponse? ResourceId,
        [property: JsonPropertyName("thumbnails")] YouTubeThumbnailsResponse? Thumbnails);

    private sealed record YouTubeThumbnailsResponse(
        [property: JsonPropertyName("default")] YouTubeThumbnailResponse? Default,
        [property: JsonPropertyName("medium")] YouTubeThumbnailResponse? Medium,
        [property: JsonPropertyName("high")] YouTubeThumbnailResponse? High,
        [property: JsonPropertyName("standard")] YouTubeThumbnailResponse? Standard,
        [property: JsonPropertyName("maxres")] YouTubeThumbnailResponse? Maxres);

    private sealed record YouTubeThumbnailResponse([property: JsonPropertyName("url")] string? Url);

    private sealed record YouTubeSuperChatEventsListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeSuperChatEventResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeSuperChatEventResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeSuperChatEventSnippetResponse? Snippet);

    private sealed record YouTubeSuperChatEventSnippetResponse(
        [property: JsonPropertyName("channelId")] string? ChannelId,
        [property: JsonPropertyName("supporterDetails")] YouTubeSuperChatSupporterDetailsResponse? SupporterDetails,
        [property: JsonPropertyName("commentText")] string? CommentText,
        [property: JsonPropertyName("amountMicros")] ulong? AmountMicros,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("displayString")] string? DisplayString,
        [property: JsonPropertyName("messageType")] long? MessageType,
        [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt);

    private sealed record YouTubeSuperChatSupporterDetailsResponse(
        [property: JsonPropertyName("channelId")] string? ChannelId,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("profileImageUrl")] string? ProfileImageUrl);

    private sealed record YouTubeLiveBroadcastsListResponse
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeLiveBroadcastResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeLiveBroadcastResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeLiveBroadcastSnippetResponse? Snippet,
        [property: JsonPropertyName("status")] YouTubeLiveBroadcastStatusResponse? Status,
        [property: JsonPropertyName("contentDetails")] YouTubeLiveBroadcastContentDetailsResponse? ContentDetails);

    private sealed record YouTubeLiveBroadcastSnippetResponse(
        [property: JsonPropertyName("channelId")] string? ChannelId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("thumbnails")] YouTubeThumbnailsResponse? Thumbnails,
        [property: JsonPropertyName("scheduledStartTime")] DateTimeOffset? ScheduledStartTime,
        [property: JsonPropertyName("actualStartTime")] DateTimeOffset? ActualStartTime,
        [property: JsonPropertyName("actualEndTime")] DateTimeOffset? ActualEndTime,
        [property: JsonPropertyName("liveChatId")] string? LiveChatId);

    private sealed record YouTubeLiveBroadcastStatusResponse([property: JsonPropertyName("lifeCycleStatus")] string? LifeCycleStatus);

    private sealed record YouTubeLiveBroadcastContentDetailsResponse(
        [property: JsonPropertyName("boundStreamId")] string? BoundStreamId);

    private sealed record YouTubeLiveStreamsListResponse
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeLiveStreamResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeLiveStreamResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeSnippetResponse? Snippet,
        [property: JsonPropertyName("status")] YouTubeLiveStreamStatusResponse? Status);

    private sealed record YouTubeLiveStreamStatusResponse([property: JsonPropertyName("streamStatus")] string? StreamStatus);

    private sealed record YouTubeSnippetResponse(
        [property: JsonPropertyName("channelId")] string? ChannelId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("publishedAt")] DateTimeOffset? PublishedAt,
        [property: JsonPropertyName("thumbnails")] YouTubeThumbnailsResponse? Thumbnails);

    private sealed record YouTubePrivacyStatusResponse(
        [property: JsonPropertyName("privacyStatus")] string? PrivacyStatus);

    private sealed record YouTubeResourceIdResponse(
        [property: JsonPropertyName("videoId")] string? VideoId,
        [property: JsonPropertyName("channelId")] string? ChannelId);
}

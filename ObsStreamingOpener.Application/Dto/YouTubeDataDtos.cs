using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record YouTubePage<TItem>(IReadOnlyList<TItem> Items, string? NextPageToken, string RawPayloadJson);

public sealed record YouTubeChannelSummary(
    string ChannelId,
    string DisplayName,
    string? Url,
    long? AudienceMemberCount,
    long? TotalViews,
    long? VideoCount,
    string? UploadsPlaylistId,
    string? Status,
    string RawPayloadJson);

public sealed record YouTubeContentItem(
    string Id,
    ProviderResourceKind ResourceKind,
    string? Title,
    string? Description,
    string? Url,
    string? Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    long? ViewCount,
    long? LikeCount,
    long? CommentCount,
    string RawPayloadJson,
    string? ChannelId = null,
    string? LiveChatId = null,
    string? BoundStreamId = null);

public sealed record YouTubeActivityEvent(
    string Id,
    string? Title,
    string? Description,
    string? ResourceId,
    string? ActivityType,
    DateTimeOffset? PublishedAt,
    string RawPayloadJson);

public sealed record YouTubeCommentEvent(
    string Id,
    string? AuthorName,
    string? AuthorChannelId,
    string? Text,
    long? LikeCount,
    DateTimeOffset? PublishedAt,
    string RawPayloadJson);

public sealed record YouTubeVisibleSubscriber(
    string ChannelId,
    string? DisplayName,
    string? ProfileUrl,
    DateTimeOffset? PublishedAt,
    string RawPayloadJson);

public sealed record YouTubeSuperChatEvent(
    string Id,
    string? ChannelId,
    string? SupporterChannelId,
    string? SupporterDisplayName,
    string? SupporterProfileImageUrl,
    string? CommentText,
    decimal? Amount,
    string? Currency,
    string? AmountDisplayString,
    long? Tier,
    DateTimeOffset? CreatedAt,
    string RawPayloadJson);

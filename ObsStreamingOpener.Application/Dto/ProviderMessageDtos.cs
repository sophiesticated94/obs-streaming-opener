using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderMessageUpsert(
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    Guid? ProviderResourceId,
    ProviderKind Provider,
    MessageSource Source,
    string? ExternalMessageId,
    string? IdentityKey,
    string? AuthorExternalId,
    string? AuthorDisplayName,
    string? AuthorProfileImageUrl,
    string? MessageText,
    DateTimeOffset PublishedAt,
    long? LikeCount,
    bool IsOwner,
    bool IsModerator,
    bool IsVerified,
    bool IsSponsor,
    decimal? Amount,
    string? Currency,
    string? PayloadSummaryJson);

public sealed record ProviderMessageDto(
    Guid Id,
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    Guid? ProviderResourceId,
    ProviderKind Provider,
    MessageSource Source,
    string? ExternalMessageId,
    string IdentityKey,
    string? AuthorExternalId,
    string? AuthorDisplayName,
    string? AuthorProfileImageUrl,
    string? MessageText,
    DateTimeOffset PublishedAt,
    long? LikeCount,
    bool IsOwner,
    bool IsModerator,
    bool IsVerified,
    bool IsSponsor,
    decimal? Amount,
    string? Currency,
    DateTimeOffset LastSeenAt);

namespace ObsStreamingOpener.Application.Dto;

public sealed record YouTubeChatMessage(
    string Id,
    string? Type,
    string? AuthorName,
    string? AuthorChannelId,
    string? AuthorProfileImageUrl,
    string? Message,
    DateTimeOffset PublishedAt,
    string RawPayloadJson,
    bool IsOwner = false,
    bool IsModerator = false,
    bool IsVerified = false,
    bool IsSponsor = false,
    decimal? Amount = null,
    string? Currency = null,
    string? AmountDisplayString = null,
    string? UserComment = null,
    long? Tier = null,
    string? StickerId = null,
    string? StickerAltText = null,
    string? MemberLevelName = null,
    bool? IsMembershipUpgrade = null,
    int? GiftMembershipsCount = null,
    string? GifterChannelId = null,
    string? AssociatedMembershipGiftingMessageId = null,
    int? MemberMonth = null)
{
    public bool IsMonetarySupport =>
        string.Equals(Type, "superChatEvent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "superStickerEvent", StringComparison.OrdinalIgnoreCase);

    public bool IsMembershipSupport =>
        string.Equals(Type, "newSponsorEvent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "memberMilestoneChatEvent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "membershipGiftingEvent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Type, "giftMembershipReceivedEvent", StringComparison.OrdinalIgnoreCase);
}

namespace ObsStreamingOpener.Application.Dto;

public sealed record YouTubeChatMessage(
    string Id,
    string? AuthorName,
    string? AuthorChannelId,
    string? Message,
    DateTimeOffset PublishedAt,
    string RawPayloadJson);

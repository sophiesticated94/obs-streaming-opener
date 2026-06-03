namespace ObsStreamingOpener.Application.Dto;

public sealed record YouTubeChatPollResult(
    IReadOnlyList<YouTubeChatMessage> Messages,
    string? NextPageToken,
    TimeSpan PollingInterval);

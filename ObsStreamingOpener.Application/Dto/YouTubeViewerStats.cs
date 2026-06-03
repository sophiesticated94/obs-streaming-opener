namespace ObsStreamingOpener.Application.Dto;

public sealed record YouTubeViewerStats(
    string VideoId,
    long? ConcurrentViewers,
    long? Likes,
    string RawPayloadJson);

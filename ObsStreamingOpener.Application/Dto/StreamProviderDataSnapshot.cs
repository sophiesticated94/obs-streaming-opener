using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record StreamProviderDataSnapshot(
    Guid MonitoredChannelId,
    ProviderKind Provider,
    Guid? StreamSessionId,
    string? ExternalStreamId,
    string? ExternalLiveChatId,
    decimal? ConcurrentViewers,
    decimal? Likes,
    DateTimeOffset CapturedAt);

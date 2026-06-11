using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record TipRealtimeDto(
    Guid Id,
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    ProviderKind Provider,
    string? ActorName,
    decimal Amount,
    string Currency,
    string? Message,
    DateTimeOffset OccurredAt);

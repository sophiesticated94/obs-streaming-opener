using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderConnectionDto(
    Guid Id,
    Guid StreamSessionId,
    ProviderKind Provider,
    string ExternalChannelId,
    string? ExternalStreamId,
    string? DisplayName);

using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderBrowserSessionDto(
    Guid Id,
    ProviderKind Provider,
    string? EncryptedStorageStateJson,
    string Status,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastValidatedAt,
    DateTimeOffset? DisconnectedAt);

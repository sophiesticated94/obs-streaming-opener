using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record MonitoredChannelDto(
    Guid Id,
    Guid MonitoredAccountId,
    ProviderKind Provider,
    string ExternalChannelId,
    string DisplayName,
    string? Url,
    bool IsDefault,
    bool IsEnabled);

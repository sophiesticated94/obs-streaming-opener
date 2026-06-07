using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record SaveAccountRequest(string DisplayName, bool IsDefault = false);

public sealed record SaveChannelSettingsRequest(
    string DisplayName,
    string? Url,
    bool IsDefault = false,
    bool IsEnabled = true);

public sealed record SaveProviderConnectionRequest(
    Guid MonitoredChannelId,
    ProviderKind Provider,
    string ExternalChannelId,
    string? ExternalStreamId,
    string? DisplayName,
    bool IsEnabled = true);

public sealed record ProviderConnectionConfigDto(
    Guid Id,
    Guid MonitoredChannelId,
    ProviderKind Provider,
    string ExternalChannelId,
    string? ExternalStreamId,
    string? DisplayName,
    bool IsEnabled);

public sealed record SaveWidgetConfigurationRequest(
    string WidgetKey,
    string WidgetType,
    string Theme,
    string SettingsJson);

public sealed record WidgetConfigurationDto(
    Guid Id,
    string WidgetKey,
    string WidgetType,
    string Theme,
    string SettingsJson,
    DateTimeOffset UpdatedAt);

public sealed record PollingConfigurationDto(
    bool EnableStreamDataPolling,
    int StreamDataPollingSeconds,
    string StreamDataSchedule,
    string AccountDataSchedule);

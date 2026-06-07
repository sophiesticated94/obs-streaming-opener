using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Options;

namespace ObsStreamingOpener.Application.Services;

public sealed class ConfigurationService(
    IConfigurationStore configurationStore,
    IChannelStore channelStore,
    IAlertRuleStore alertRuleStore,
    IProviderCredentialStore credentialStore,
    IOptions<StreamingMonitorOptions> streamingOptions) : IConfigurationService
{
    public Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
        => channelStore.GetAccountsAsync(cancellationToken);

    public Task<IReadOnlyList<ConnectedAccountDto>> GetConnectedAccountsAsync(CancellationToken cancellationToken = default)
        => credentialStore.GetConnectedAccountsAsync(cancellationToken);

    public Task<MonitoredAccountDto> CreateAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.DisplayName, nameof(request.DisplayName));
        return configurationStore.CreateAccountAsync(request, cancellationToken);
    }

    public Task<MonitoredAccountDto?> UpdateAccountAsync(Guid accountId, SaveAccountRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.DisplayName, nameof(request.DisplayName));
        return configurationStore.UpdateAccountAsync(accountId, request, cancellationToken);
    }

    public Task<IReadOnlyList<MonitoredChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default)
        => channelStore.GetChannelsAsync(cancellationToken);

    public Task<MonitoredChannelDto?> UpdateChannelAsync(Guid channelId, SaveChannelSettingsRequest request, CancellationToken cancellationToken = default)
    {
        ValidateChannel(request);
        return configurationStore.UpdateChannelAsync(channelId, request, cancellationToken);
    }

    public Task<IReadOnlyList<ProviderConnectionConfigDto>> GetProviderConnectionsAsync(Guid? channelId = null, CancellationToken cancellationToken = default)
        => configurationStore.GetProviderConnectionsAsync(channelId, cancellationToken);

    public Task<ProviderConnectionConfigDto> CreateProviderConnectionAsync(SaveProviderConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateProviderConnection(request);
        return configurationStore.CreateProviderConnectionAsync(request, cancellationToken);
    }

    public Task<ProviderConnectionConfigDto?> UpdateProviderConnectionAsync(Guid providerConnectionId, SaveProviderConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateProviderConnection(request);
        return configurationStore.UpdateProviderConnectionAsync(providerConnectionId, request, cancellationToken);
    }

    public Task<bool> DeleteProviderConnectionAsync(Guid providerConnectionId, CancellationToken cancellationToken = default)
        => configurationStore.DeleteProviderConnectionAsync(providerConnectionId, cancellationToken);

    public Task<IReadOnlyList<WidgetConfigurationDto>> GetWidgetConfigurationsAsync(CancellationToken cancellationToken = default)
        => configurationStore.GetWidgetConfigurationsAsync(cancellationToken);

    public Task<WidgetConfigurationDto> UpsertWidgetConfigurationAsync(SaveWidgetConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequired(request.WidgetKey, nameof(request.WidgetKey));
        ValidateRequired(request.WidgetType, nameof(request.WidgetType));
        ValidateRequired(request.Theme, nameof(request.Theme));
        ValidateJsonObject(request.SettingsJson, nameof(request.SettingsJson));
        return configurationStore.UpsertWidgetConfigurationAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<AlertRuleDto>> GetAlertRulesAsync(Guid? monitoredChannelId = null, CancellationToken cancellationToken = default)
        => alertRuleStore.GetAlertRulesAsync(monitoredChannelId, cancellationToken);

    public Task<AlertRuleDto> UpsertAlertRuleAsync(SaveAlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MonitoredChannelId == Guid.Empty)
        {
            throw new ArgumentException("MonitoredChannelId is required.", nameof(request));
        }

        ValidateRequired(request.VisualStyle, nameof(request.VisualStyle));
        if (request.DurationSeconds is < 1 or > 60)
        {
            throw new ArgumentException("DurationSeconds must be between 1 and 60.", nameof(request));
        }

        return alertRuleStore.UpsertAlertRuleAsync(request, cancellationToken);
    }

    public PollingConfigurationDto GetPollingConfiguration()
    {
        var options = streamingOptions.Value;
        return new PollingConfigurationDto(
            options.EnableStreamDataPolling,
            Math.Max(5, options.StreamDataPollingSeconds),
            "Every 5 seconds when enabled, clamped by StreamingMonitor:StreamDataPollingSeconds",
            "Hangfire recurring job: minutely");
    }

    private static void ValidateChannel(SaveChannelSettingsRequest request)
    {
        ValidateRequired(request.DisplayName, nameof(request.DisplayName));
    }

    private static void ValidateProviderConnection(SaveProviderConnectionRequest request)
    {
        if (request.MonitoredChannelId == Guid.Empty)
        {
            throw new ArgumentException("MonitoredChannelId is required.", nameof(request));
        }

        ValidateRequired(request.ExternalChannelId, nameof(request.ExternalChannelId));
    }

    private static void ValidateRequired(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }
    }

    private static void ValidateJsonObject(string value, string name)
    {
        ValidateRequired(value, name);
        using var document = System.Text.Json.JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            throw new ArgumentException($"{name} must be a JSON object.", name);
        }
    }
}

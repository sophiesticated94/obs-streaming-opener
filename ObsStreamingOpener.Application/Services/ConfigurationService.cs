using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Options;
using System.Text.Json;

namespace ObsStreamingOpener.Application.Services;

public sealed class ConfigurationService(
    IConfigurationStore configurationStore,
    IChannelStore channelStore,
    IAlertRuleStore alertRuleStore,
    IProviderCredentialStore credentialStore,
    IOptions<StreamingMonitorOptions> streamingOptions) : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AlertWidgetSettingsDto DefaultAlertWidgetSettings = new(
        "default",
        "shortest-first",
        1000,
        60000,
        null,
        null,
        "sparkles",
        0.8m,
        true);

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

    public async Task<AlertWidgetSettingsDto> GetAlertWidgetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var widgets = await configurationStore.GetWidgetConfigurationsAsync(cancellationToken);
        var widget = widgets.FirstOrDefault(x => x.WidgetKey.Equals("alerts", StringComparison.OrdinalIgnoreCase));
        if (widget is null || string.IsNullOrWhiteSpace(widget.SettingsJson))
        {
            return DefaultAlertWidgetSettings;
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AlertWidgetSettingsDto>(widget.SettingsJson, JsonOptions)
                ?? DefaultAlertWidgetSettings;
            return NormalizeAlertWidgetSettings(settings);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return DefaultAlertWidgetSettings;
        }
    }

    public async Task<AlertWidgetSettingsDto> UpsertAlertWidgetSettingsAsync(AlertWidgetSettingsDto request, CancellationToken cancellationToken = default)
    {
        var settings = NormalizeAlertWidgetSettings(request);
        await configurationStore.UpsertWidgetConfigurationAsync(new SaveWidgetConfigurationRequest(
            "alerts",
            "alerts",
            settings.Theme,
            JsonSerializer.Serialize(settings, JsonOptions)), cancellationToken);
        return settings;
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

    private static AlertWidgetSettingsDto NormalizeAlertWidgetSettings(AlertWidgetSettingsDto request)
    {
        var theme = NormalizeText(request.Theme) ?? "default";
        var queueOrdering = NormalizeText(request.QueueOrdering) ?? "shortest-first";
        if (!queueOrdering.Equals("shortest-first", StringComparison.OrdinalIgnoreCase)
            && !queueOrdering.Equals("oldest-first", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("QueueOrdering must be shortest-first or oldest-first.", nameof(request));
        }

        if (request.MinDurationMs is < 500 or > 60000)
        {
            throw new ArgumentException("MinDurationMs must be between 500 and 60000.", nameof(request));
        }

        if (request.MaxDurationMs is < 500 or > 60000 || request.MaxDurationMs < request.MinDurationMs)
        {
            throw new ArgumentException("MaxDurationMs must be between 500 and 60000 and greater than or equal to MinDurationMs.", nameof(request));
        }

        if (request.Volume is < 0 or > 1)
        {
            throw new ArgumentException("Volume must be between 0 and 1.", nameof(request));
        }

        var defaultSoundUrl = NormalizeUrlPath(request.DefaultSoundUrl, nameof(request.DefaultSoundUrl));
        var defaultMediaUrl = NormalizeUrlPath(request.DefaultMediaUrl, nameof(request.DefaultMediaUrl));
        return new AlertWidgetSettingsDto(
            theme,
            queueOrdering.ToLowerInvariant(),
            request.MinDurationMs,
            request.MaxDurationMs,
            defaultSoundUrl,
            defaultMediaUrl,
            NormalizeText(request.AnimationPreset) ?? "sparkles",
            request.Volume,
            request.AutoAck);
    }

    private static string? NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeUrlPath(string? value, string name)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return normalized;
        }

        throw new ArgumentException($"{name} must be an absolute http(s) URL or an app-root-relative path.", name);
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

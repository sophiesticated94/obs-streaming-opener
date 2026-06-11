using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IConfigurationService
{
    Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectedAccountDto>> GetConnectedAccountsAsync(CancellationToken cancellationToken = default);

    Task<MonitoredAccountDto> CreateAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default);

    Task<MonitoredAccountDto?> UpdateAccountAsync(Guid accountId, SaveAccountRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonitoredChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default);

    Task<MonitoredChannelDto?> UpdateChannelAsync(Guid channelId, SaveChannelSettingsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderConnectionConfigDto>> GetProviderConnectionsAsync(Guid? channelId = null, CancellationToken cancellationToken = default);

    Task<ProviderConnectionConfigDto> CreateProviderConnectionAsync(SaveProviderConnectionRequest request, CancellationToken cancellationToken = default);

    Task<ProviderConnectionConfigDto?> UpdateProviderConnectionAsync(Guid providerConnectionId, SaveProviderConnectionRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteProviderConnectionAsync(Guid providerConnectionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WidgetConfigurationDto>> GetWidgetConfigurationsAsync(CancellationToken cancellationToken = default);

    Task<WidgetConfigurationDto> UpsertWidgetConfigurationAsync(SaveWidgetConfigurationRequest request, CancellationToken cancellationToken = default);

    Task<AlertWidgetSettingsDto> GetAlertWidgetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AlertWidgetSettingsDto> UpsertAlertWidgetSettingsAsync(AlertWidgetSettingsDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRuleDto>> GetAlertRulesAsync(Guid? monitoredChannelId = null, CancellationToken cancellationToken = default);

    Task<AlertRuleDto> UpsertAlertRuleAsync(SaveAlertRuleRequest request, CancellationToken cancellationToken = default);

    PollingConfigurationDto GetPollingConfiguration();
}

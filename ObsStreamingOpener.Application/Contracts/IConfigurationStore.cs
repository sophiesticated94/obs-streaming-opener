using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IConfigurationStore
{
    Task<MonitoredAccountDto> CreateAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default);

    Task<MonitoredAccountDto?> UpdateAccountAsync(Guid accountId, SaveAccountRequest request, CancellationToken cancellationToken = default);

    Task<MonitoredChannelDto?> UpdateChannelAsync(Guid channelId, SaveChannelSettingsRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderConnectionConfigDto>> GetProviderConnectionsAsync(Guid? channelId = null, CancellationToken cancellationToken = default);

    Task<ProviderConnectionConfigDto> CreateProviderConnectionAsync(SaveProviderConnectionRequest request, CancellationToken cancellationToken = default);

    Task<ProviderConnectionConfigDto?> UpdateProviderConnectionAsync(Guid providerConnectionId, SaveProviderConnectionRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteProviderConnectionAsync(Guid providerConnectionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WidgetConfigurationDto>> GetWidgetConfigurationsAsync(CancellationToken cancellationToken = default);

    Task<WidgetConfigurationDto> UpsertWidgetConfigurationAsync(SaveWidgetConfigurationRequest request, CancellationToken cancellationToken = default);
}

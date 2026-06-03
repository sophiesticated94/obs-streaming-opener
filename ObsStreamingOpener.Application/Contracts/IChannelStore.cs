using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IChannelStore
{
    Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonitoredChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default);

    Task<MonitoredChannelDto?> GetChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);

    Task<MonitoredChannel> GetDefaultChannelEntityAsync(CancellationToken cancellationToken = default);

    Task<MonitoredChannelDto> GetDefaultChannelAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderConnectionDto>> GetEnabledConnectionsAsync(ProviderKind? provider = null, CancellationToken cancellationToken = default);
}

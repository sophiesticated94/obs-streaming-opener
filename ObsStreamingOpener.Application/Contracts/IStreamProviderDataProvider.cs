using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStreamProviderDataProvider
{
    Task<StreamProviderDataSnapshot?> GetCurrentStreamDataAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);
}

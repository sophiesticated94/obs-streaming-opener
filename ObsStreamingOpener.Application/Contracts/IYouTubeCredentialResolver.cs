using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IYouTubeCredentialResolver
{
    Task<ProviderAccessTokenDto?> ResolveForChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);
}

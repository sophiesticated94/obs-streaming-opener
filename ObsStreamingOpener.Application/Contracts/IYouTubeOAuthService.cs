using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IYouTubeOAuthService
{
    YouTubeAuthorizationUrlDto Start(Guid? accountId = null);

    Task<Guid> CompleteCallbackAsync(string code, string state, CancellationToken cancellationToken = default);

    Task<YouTubeAuthorizationUrlDto> ReloginAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<ConnectedAccountDto?> RefreshAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<ConnectedAccountDto?> SyncAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<bool> DisconnectAsync(Guid accountId, CancellationToken cancellationToken = default);
}

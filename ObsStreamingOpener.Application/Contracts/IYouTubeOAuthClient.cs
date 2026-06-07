using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IYouTubeOAuthClient
{
    Task<YouTubeTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default);

    Task<YouTubeTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<YouTubeUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<YouTubeChannelInfo>> GetMyChannelsAsync(string accessToken, CancellationToken cancellationToken = default);
}

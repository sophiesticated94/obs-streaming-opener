using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Services;

public sealed class YouTubeCredentialResolver(
    IProviderCredentialStore credentialStore,
    ICredentialProtector credentialProtector,
    IYouTubeOAuthService youtubeOAuthService,
    IClock clock) : IYouTubeCredentialResolver
{
    public async Task<ProviderAccessTokenDto?> ResolveForChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var credential = await credentialStore.GetYouTubeCredentialForChannelAsync(monitoredChannelId, cancellationToken);
        if (credential is null || credential.DisconnectedAt is not null)
        {
            return null;
        }

        if (credential.AccessTokenExpiresAt <= clock.UtcNow.AddMinutes(1) && credential.EncryptedRefreshToken is not null)
        {
            await youtubeOAuthService.RefreshAsync(credential.MonitoredAccountId, cancellationToken);
            credential = await credentialStore.GetYouTubeCredentialForChannelAsync(monitoredChannelId, cancellationToken);
        }

        if (credential?.EncryptedAccessToken is null || credential.DisconnectedAt is not null)
        {
            return null;
        }

        return new ProviderAccessTokenDto(
            credential.MonitoredAccountId,
            credentialProtector.Unprotect(credential.EncryptedAccessToken));
    }
}

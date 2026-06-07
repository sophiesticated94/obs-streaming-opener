using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderCredentialStore
{
    Task<IReadOnlyList<ConnectedAccountDto>> GetConnectedAccountsAsync(CancellationToken cancellationToken = default);

    Task<StoredProviderCredentialDto?> GetYouTubeCredentialAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<StoredProviderCredentialDto?> GetYouTubeCredentialForChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);

    Task<Guid> UpsertYouTubeAccountAsync(UpsertYouTubeAccountRequest request, CancellationToken cancellationToken = default);

    Task UpdateYouTubeCredentialTokensAsync(
        Guid accountId,
        string encryptedAccessToken,
        string? encryptedRefreshToken,
        DateTimeOffset accessTokenExpiresAt,
        string? tokenType,
        string scopes,
        CancellationToken cancellationToken = default);

    Task<bool> DisconnectYouTubeCredentialAsync(Guid accountId, CancellationToken cancellationToken = default);
}

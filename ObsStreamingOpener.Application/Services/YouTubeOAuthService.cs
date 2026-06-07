using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Options;

namespace ObsStreamingOpener.Application.Services;

public sealed class YouTubeOAuthService(
    IOptions<YouTubeOAuthOptions> options,
    IClock clock,
    IDataProtectionProvider dataProtectionProvider,
    ICredentialProtector credentialProtector,
    IYouTubeOAuthClient oauthClient,
    IProviderCredentialStore credentialStore) : IYouTubeOAuthService
{
    private static readonly string[] Scopes =
    [
        "openid",
        "email",
        "profile",
        "https://www.googleapis.com/auth/youtube.readonly"
    ];

    private readonly YouTubeOAuthOptions _options = options.Value;
    private readonly IDataProtector _stateProtector = dataProtectionProvider.CreateProtector("ObsStreamingOpener.YouTubeOAuthState.v1");

    public YouTubeAuthorizationUrlDto Start(Guid? accountId = null)
    {
        EnsureConfigured();
        var state = ProtectState(new OAuthStatePayload(accountId, Guid.NewGuid().ToString("N"), clock.UtcNow));
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = string.Join(' ', Scopes),
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };

        return new YouTubeAuthorizationUrlDto($"https://accounts.google.com/o/oauth2/v2/auth?{BuildQuery(query)}");
    }

    public async Task<Guid> CompleteCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("OAuth code is required.", nameof(code));
        }

        var statePayload = UnprotectState(state);
        var token = await oauthClient.ExchangeCodeAsync(code, _options.RedirectUri, cancellationToken);
        return await UpsertFromTokenAsync(statePayload.AccountId, token, cancellationToken);
    }

    public Task<YouTubeAuthorizationUrlDto> ReloginAsync(Guid accountId, CancellationToken cancellationToken = default)
        => Task.FromResult(Start(accountId));

    public async Task<ConnectedAccountDto?> RefreshAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var credential = await credentialStore.GetYouTubeCredentialAsync(accountId, cancellationToken);
        if (credential?.EncryptedRefreshToken is null)
        {
            return null;
        }

        var refreshToken = credentialProtector.Unprotect(credential.EncryptedRefreshToken);
        var token = await oauthClient.RefreshTokenAsync(refreshToken, cancellationToken);
        await credentialStore.UpdateYouTubeCredentialTokensAsync(
            accountId,
            credentialProtector.Protect(token.AccessToken),
            token.RefreshToken is null ? null : credentialProtector.Protect(token.RefreshToken),
            clock.UtcNow.AddSeconds(Math.Max(token.ExpiresIn, 0)),
            token.TokenType,
            token.Scope,
            cancellationToken);

        return (await credentialStore.GetConnectedAccountsAsync(cancellationToken)).FirstOrDefault(x => x.AccountId == accountId);
    }

    public async Task<ConnectedAccountDto?> SyncAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var credential = await GetUsableCredentialAsync(accountId, cancellationToken);
        if (credential?.EncryptedAccessToken is null)
        {
            return null;
        }

        var accessToken = credentialProtector.Unprotect(credential.EncryptedAccessToken);
        var userInfo = await oauthClient.GetUserInfoAsync(accessToken, cancellationToken);
        var channels = await oauthClient.GetMyChannelsAsync(accessToken, cancellationToken);
        await credentialStore.UpsertYouTubeAccountAsync(new UpsertYouTubeAccountRequest(
            accountId,
            userInfo,
            channels,
            credential.EncryptedAccessToken,
            credential.EncryptedRefreshToken,
            credential.AccessTokenExpiresAt ?? clock.UtcNow,
            credential.TokenType,
            credential.Scopes), cancellationToken);

        return (await credentialStore.GetConnectedAccountsAsync(cancellationToken)).FirstOrDefault(x => x.AccountId == accountId);
    }

    public Task<bool> DisconnectAsync(Guid accountId, CancellationToken cancellationToken = default)
        => credentialStore.DisconnectYouTubeCredentialAsync(accountId, cancellationToken);

    private async Task<Guid> UpsertFromTokenAsync(Guid? accountId, YouTubeTokenResponse token, CancellationToken cancellationToken)
    {
        var userInfo = await oauthClient.GetUserInfoAsync(token.AccessToken, cancellationToken);
        var channels = await oauthClient.GetMyChannelsAsync(token.AccessToken, cancellationToken);
        return await credentialStore.UpsertYouTubeAccountAsync(new UpsertYouTubeAccountRequest(
            accountId,
            userInfo,
            channels,
            credentialProtector.Protect(token.AccessToken),
            token.RefreshToken is null ? null : credentialProtector.Protect(token.RefreshToken),
            clock.UtcNow.AddSeconds(Math.Max(token.ExpiresIn, 0)),
            token.TokenType,
            token.Scope), cancellationToken);
    }

    private async Task<StoredProviderCredentialDto?> GetUsableCredentialAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var credential = await credentialStore.GetYouTubeCredentialAsync(accountId, cancellationToken);
        if (credential is null)
        {
            return null;
        }

        if (credential.AccessTokenExpiresAt <= clock.UtcNow.AddMinutes(1) && credential.EncryptedRefreshToken is not null)
        {
            await RefreshAsync(accountId, cancellationToken);
            return await credentialStore.GetYouTubeCredentialAsync(accountId, cancellationToken);
        }

        return credential;
    }

    private string ProtectState(OAuthStatePayload payload)
        => Base64UrlEncode(_stateProtector.Protect(JsonSerializer.Serialize(payload)));

    private OAuthStatePayload UnprotectState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("OAuth state is required.", nameof(state));
        }

        var payload = JsonSerializer.Deserialize<OAuthStatePayload>(_stateProtector.Unprotect(Base64UrlDecode(state)))
            ?? throw new ArgumentException("OAuth state is invalid.", nameof(state));
        if (payload.CreatedAt < clock.UtcNow.AddMinutes(-15))
        {
            throw new ArgumentException("OAuth state has expired.", nameof(state));
        }

        return payload;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("YouTube OAuth client id/secret are not configured.");
        }
    }

    private static string BuildQuery(Dictionary<string, string?> values)
        => string.Join('&', values.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value ?? string.Empty)}"));

    private static string Base64UrlEncode(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}

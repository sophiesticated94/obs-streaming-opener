using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record YouTubeAuthorizationUrlDto(string AuthorizationUrl);

public sealed record ConnectedAccountDto(
    Guid AccountId,
    string DisplayName,
    ProviderKind Provider,
    string? ExternalAccountId,
    string? Email,
    int ChannelCount,
    bool IsLoggedIn,
    bool HasRefreshToken,
    DateTimeOffset? AccessTokenExpiresAt,
    bool IsExpired,
    DateTimeOffset? LastRefreshedAt,
    DateTimeOffset? DisconnectedAt,
    string Scopes);

public sealed record OAuthStatePayload(Guid? AccountId, string Nonce, DateTimeOffset CreatedAt);

public sealed record YouTubeTokenResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn,
    string? TokenType,
    string Scope);

public sealed record YouTubeUserInfo(
    string ExternalAccountId,
    string? Email,
    string? DisplayName);

public sealed record YouTubeChannelInfo(
    string ChannelId,
    string DisplayName,
    string? Url,
    long? AudienceMemberCount,
    long? TotalViews,
    string RawPayloadJson,
    long? VideoCount = null,
    string? UploadsPlaylistId = null,
    string? Status = null);

public sealed record UpsertYouTubeAccountRequest(
    Guid? ExistingAccountId,
    YouTubeUserInfo UserInfo,
    IReadOnlyList<YouTubeChannelInfo> Channels,
    string EncryptedAccessToken,
    string? EncryptedRefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string? TokenType,
    string Scopes);

public sealed record StoredProviderCredentialDto(
    Guid Id,
    Guid MonitoredAccountId,
    ProviderKind Provider,
    string ExternalAccountId,
    string? Email,
    string? DisplayName,
    string? EncryptedAccessToken,
    string? EncryptedRefreshToken,
    DateTimeOffset? AccessTokenExpiresAt,
    string? TokenType,
    string Scopes,
    DateTimeOffset ConnectedAt,
    DateTimeOffset? LastRefreshedAt,
    DateTimeOffset? DisconnectedAt);

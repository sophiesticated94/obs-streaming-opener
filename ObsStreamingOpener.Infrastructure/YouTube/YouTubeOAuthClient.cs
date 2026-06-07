using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Options;
using ObsStreamingOpener.Infrastructure.Http;

namespace ObsStreamingOpener.Infrastructure.YouTube;

public sealed class YouTubeOAuthClient(
    IExternalHttpClient httpClient,
    IOptions<YouTubeOAuthOptions> options) : IYouTubeOAuthClient
{
    private const string ServiceName = "Google/YouTube";
    private readonly YouTubeOAuthOptions _options = options.Value;

    public async Task<YouTubeTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId ?? string.Empty,
                ["client_secret"] = _options.ClientSecret ?? string.Empty,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            })
        };

        return ToTokenResponse(await httpClient.SendAsync<GoogleTokenResponse>(request, ServiceName, cancellationToken: cancellationToken));
    }

    public async Task<YouTubeTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId ?? string.Empty,
                ["client_secret"] = _options.ClientSecret ?? string.Empty,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            })
        };

        return ToTokenResponse(await httpClient.SendAsync<GoogleTokenResponse>(request, ServiceName, cancellationToken: cancellationToken));
    }

    public async Task<YouTubeUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var userInfo = await httpClient.SendAsync<GoogleUserInfoResponse>(request, ServiceName, cancellationToken: cancellationToken);
        return new YouTubeUserInfo(
            userInfo.Sub ?? throw new InvalidOperationException("Google userinfo response did not include sub."),
            userInfo.Email,
            userInfo.Name);
    }

    public async Task<IReadOnlyList<YouTubeChannelInfo>> GetMyChannelsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/channels?part=id,snippet,statistics,contentDetails,brandingSettings,status&mine=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendWithBodyAsync<YouTubeChannelsListResponse>(request, ServiceName, cancellationToken: cancellationToken);
        var channels = new List<YouTubeChannelInfo>();
        foreach (var item in response.Body.Items)
        {
            var id = item.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            channels.Add(new YouTubeChannelInfo(
                id,
                item.Snippet?.Title ?? id,
                $"https://www.youtube.com/channel/{id}",
                ParseLong(item.Statistics?.SubscriberCount),
                ParseLong(item.Statistics?.ViewCount),
                response.RawBody,
                ParseLong(item.Statistics?.VideoCount),
                item.ContentDetails?.RelatedPlaylists?.Uploads,
                item.Status?.PrivacyStatus));
        }

        return channels;
    }

    private static long? ParseLong(string? value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static YouTubeTokenResponse ToTokenResponse(GoogleTokenResponse token)
        => new(
            token.AccessToken ?? throw new InvalidOperationException("OAuth response did not include access_token."),
            token.RefreshToken,
            token.ExpiresIn ?? 3600,
            token.TokenType,
            token.Scope ?? string.Empty);

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record GoogleUserInfoResponse(
        [property: JsonPropertyName("sub")] string? Sub,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record YouTubeChannelsListResponse
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<YouTubeChannelItemResponse> Items { get; init; } = [];
    }

    private sealed record YouTubeChannelItemResponse(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("snippet")] YouTubeChannelSnippetResponse? Snippet,
        [property: JsonPropertyName("statistics")] YouTubeChannelStatisticsResponse? Statistics,
        [property: JsonPropertyName("contentDetails")] YouTubeChannelContentDetailsResponse? ContentDetails,
        [property: JsonPropertyName("status")] YouTubeChannelStatusResponse? Status);

    private sealed record YouTubeChannelSnippetResponse(
        [property: JsonPropertyName("title")] string? Title);

    private sealed record YouTubeChannelStatisticsResponse(
        [property: JsonPropertyName("subscriberCount")] string? SubscriberCount,
        [property: JsonPropertyName("viewCount")] string? ViewCount,
        [property: JsonPropertyName("videoCount")] string? VideoCount);

    private sealed record YouTubeChannelContentDetailsResponse(
        [property: JsonPropertyName("relatedPlaylists")] YouTubeRelatedPlaylistsResponse? RelatedPlaylists);

    private sealed record YouTubeRelatedPlaylistsResponse(
        [property: JsonPropertyName("uploads")] string? Uploads);

    private sealed record YouTubeChannelStatusResponse(
        [property: JsonPropertyName("privacyStatus")] string? PrivacyStatus);
}

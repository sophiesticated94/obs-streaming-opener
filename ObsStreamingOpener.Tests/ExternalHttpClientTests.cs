using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Exceptions;
using ObsStreamingOpener.Infrastructure.Http;
using ObsStreamingOpener.Infrastructure.Options;
using ObsStreamingOpener.Infrastructure.YouTube;

namespace ObsStreamingOpener.Tests;

public sealed class ExternalHttpClientTests
{
    [Fact]
    public async Task SendAsync_DeserializesSuccessfulJsonResponse()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"name\":\"ok\"}", Encoding.UTF8, "application/json")
        });

        var result = await client.SendAsync<TestResponse>(new HttpRequestMessage(HttpMethod.Get, "https://example.test/ok"), "Test");

        Assert.Equal("ok", result.Name);
    }

    [Fact]
    public async Task SendAsync_FailedOAuthResponseIncludesGoogleErrorDetails()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            ReasonPhrase = "Bad Request",
            Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"Bad authorization code\"}", Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<ExternalHttpRequestException>(() =>
            client.SendAsync<TestResponse>(new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token"), "Google"));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("invalid_grant", ex.ProviderErrorCode);
        Assert.Equal("Bad authorization code", ex.ProviderErrorMessage);
        Assert.Contains("invalid_grant", ex.Message);
        Assert.Contains("Bad authorization code", ex.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_FailedGoogleApiResponseIncludesNestedErrorDetailsAndRequestIds()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            ReasonPhrase = "Forbidden",
            Content = new StringContent(
                "{\"error\":{\"code\":403,\"message\":\"YouTube Data API v3 has not been used\",\"status\":\"PERMISSION_DENIED\",\"errors\":[{\"reason\":\"accessNotConfigured\"}]}}",
                Encoding.UTF8,
                "application/json")
        };
        response.Headers.TryAddWithoutValidation("x-goog-request-id", "google-request-1");
        var client = CreateClient(response);

        var ex = await Assert.ThrowsAsync<ExternalHttpRequestException>(() =>
            client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/channels?key=secret-api-key"), "YouTube"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal("PERMISSION_DENIED", ex.ProviderErrorCode);
        Assert.Equal("YouTube Data API v3 has not been used", ex.ProviderErrorMessage);
        Assert.Contains("YouTube Data API v3 has not been used", ex.ResponseBody);
        Assert.Equal("google-request-1", ex.RequestIds["x-goog-request-id"]);
        Assert.DoesNotContain("secret-api-key", ex.RequestUri);
        Assert.Contains("key=REDACTED", ex.RequestUri);
    }

    [Fact]
    public async Task SendAsync_MalformedSuccessfulJsonThrowsReadableDeserializationException()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{", Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<ExternalHttpDeserializationException>(() =>
            client.SendAsync<TestResponse>(new HttpRequestMessage(HttpMethod.Get, "https://example.test/malformed"), "Test"));

        Assert.Equal("Test", ex.ServiceName);
        Assert.Equal(typeof(TestResponse), ex.ResponseType);
        Assert.Equal("{", ex.ResponseBody);
    }

    [Fact]
    public async Task YouTubeOAuthClient_GetMyChannelsAsync_ThrowsReadableExceptionForForbiddenResponse()
    {
        var externalClient = CreateClient(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                "{\"error\":{\"code\":403,\"message\":\"Insufficient Permission\",\"status\":\"PERMISSION_DENIED\"}}",
                Encoding.UTF8,
                "application/json")
        });
        var client = new YouTubeOAuthClient(
            externalClient,
            Options.Create(new ObsStreamingOpener.Application.Options.YouTubeOAuthOptions
            {
                ClientId = "client",
                ClientSecret = "secret"
            }));

        var ex = await Assert.ThrowsAsync<ExternalHttpRequestException>(() => client.GetMyChannelsAsync("access-token"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal("PERMISSION_DENIED", ex.ProviderErrorCode);
        Assert.Equal("Insufficient Permission", ex.ProviderErrorMessage);
        Assert.Contains("Insufficient Permission", ex.ResponseBody);
    }

    [Fact]
    public async Task YouTubeApiClient_UsesBearerTokenAndRedactsApiKeyOnFailure()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":{\"message\":\"quota exceeded\",\"status\":\"PERMISSION_DENIED\"}}", Encoding.UTF8, "application/json")
        });
        var externalClient = new ExternalHttpClient(new HttpClient(handler));
        var client = new YouTubeApiClient(
            externalClient,
            Options.Create(new YouTubeOptions
            {
                ApiKey = "secret-api-key",
                BaseUrl = "https://www.googleapis.com/youtube/v3/"
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<YouTubeApiClient>.Instance);

        var ex = await Assert.ThrowsAsync<ExternalHttpRequestException>(() => client.GetViewerStatsAsync("video-id", "access-token"));

        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", handler.LastRequest?.Headers.Authorization?.Parameter);
        Assert.DoesNotContain("access-token", ex.RequestUri);
        Assert.DoesNotContain("secret-api-key", ex.RequestUri);
    }

    private static ExternalHttpClient CreateClient(HttpResponseMessage response)
        => new(new HttpClient(new RecordingHandler(response)));

    private sealed record TestResponse([property: JsonPropertyName("name")] string Name);

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}

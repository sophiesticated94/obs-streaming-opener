using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ObsStreamingOpener.Application.Exceptions;

namespace ObsStreamingOpener.Infrastructure.Http;

public sealed class ExternalHttpClient(HttpClient httpClient) : IExternalHttpClient
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token",
        "refresh_token",
        "code",
        "client_secret",
        "key",
        "api_key"
    };

    public async Task<TResponse> SendAsync<TResponse>(
        HttpRequestMessage request,
        string serviceName,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
        => (await SendWithBodyAsync<TResponse>(request, serviceName, jsonOptions, cancellationToken)).Body;

    public async Task<ExternalHttpResponse<TResponse>> SendWithBodyAsync<TResponse>(
        HttpRequestMessage request,
        string serviceName,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var responseBody = await SendAsync(request, serviceName, cancellationToken);
        try
        {
            var body = JsonSerializer.Deserialize<TResponse>(responseBody, jsonOptions ?? DefaultJsonOptions)
                ?? throw new JsonException($"Response JSON was empty for {typeof(TResponse).Name}.");
            return new ExternalHttpResponse<TResponse>(body, responseBody);
        }
        catch (JsonException ex)
        {
            throw new ExternalHttpDeserializationException(
                serviceName,
                request.Method,
                RedactUri(request.RequestUri),
                typeof(TResponse),
                responseBody,
                ex);
        }
    }

    public async Task<string> SendAsync(
        HttpRequestMessage request,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var requestUri = RedactUri(response.RequestMessage?.RequestUri ?? request.RequestUri);

        if (response.IsSuccessStatusCode)
        {
            return responseBody;
        }

        var providerError = ParseProviderError(responseBody);
        throw new ExternalHttpRequestException(
            serviceName,
            response.RequestMessage?.Method ?? request.Method,
            requestUri,
            response.StatusCode,
            response.ReasonPhrase,
            responseBody,
            providerError.Code,
            providerError.Message,
            ExtractRequestIds(response.Headers));
    }

    private static (string? Code, string? Message) ParseProviderError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return (null, null);
        }

        var oauthError = TryDeserialize<OAuthErrorResponse>(responseBody);
        if (!string.IsNullOrWhiteSpace(oauthError?.Error))
        {
            return (oauthError.Error, oauthError.ErrorDescription ?? oauthError.Error);
        }

        var apiError = TryDeserialize<GoogleApiErrorResponse>(responseBody);
        if (apiError?.Error is null)
        {
            return (null, null);
        }

        var detail = apiError.Error.Errors?.FirstOrDefault();
        return (
            apiError.Error.Status ?? apiError.Error.Code?.ToString() ?? detail?.Reason,
            apiError.Error.Message ?? detail?.Message);
    }

    private static TResponse? TryDeserialize<TResponse>(string responseBody)
    {
        try
        {
            return JsonSerializer.Deserialize<TResponse>(responseBody, DefaultJsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static IReadOnlyDictionary<string, string> ExtractRequestIds(HttpResponseHeaders headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var headerName in new[] { "x-request-id", "x-correlation-id", "x-goog-request-id", "x-guploader-uploadid" })
        {
            if (headers.TryGetValues(headerName, out var values))
            {
                result[headerName] = string.Join(",", values);
            }
        }

        return result;
    }

    private static string RedactUri(Uri? uri)
    {
        if (uri is null)
        {
            return string.Empty;
        }

        if (!uri.IsAbsoluteUri)
        {
            var relative = uri.ToString();
            var queryIndex = relative.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex < 0)
            {
                return relative;
            }

            var path = relative[..queryIndex];
            var query = relative[(queryIndex + 1)..]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(RedactQueryParameter);
            return $"{path}?{string.Join('&', query)}";
        }

        var builder = new UriBuilder(uri);
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            var query = builder.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(RedactQueryParameter);
            builder.Query = string.Join('&', query);
        }

        return builder.Uri.ToString();
    }

    private static string RedactQueryParameter(string parameter)
    {
        var parts = parameter.Split('=', 2);
        var key = Uri.UnescapeDataString(parts[0]);
        if (!SensitiveQueryKeys.Contains(key))
        {
            return parameter;
        }

        return $"{parts[0]}=REDACTED";
    }

    private sealed record OAuthErrorResponse(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);

    private sealed record GoogleApiErrorResponse(
        [property: JsonPropertyName("error")] GoogleApiError? Error);

    private sealed record GoogleApiError(
        [property: JsonPropertyName("code")] int? Code,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("errors")] IReadOnlyList<GoogleApiErrorDetail>? Errors);

    private sealed record GoogleApiErrorDetail(
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("message")] string? Message);
}

using System.Text.Json;

namespace ObsStreamingOpener.Infrastructure.Http;

public interface IExternalHttpClient
{
    Task<TResponse> SendAsync<TResponse>(
        HttpRequestMessage request,
        string serviceName,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    Task<ExternalHttpResponse<TResponse>> SendWithBodyAsync<TResponse>(
        HttpRequestMessage request,
        string serviceName,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    Task<string> SendAsync(
        HttpRequestMessage request,
        string serviceName,
        CancellationToken cancellationToken = default);
}

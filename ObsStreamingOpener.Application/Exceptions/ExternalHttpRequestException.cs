using System.Net;

namespace ObsStreamingOpener.Application.Exceptions;

public sealed class ExternalHttpRequestException : Exception
{
    public ExternalHttpRequestException(
        string serviceName,
        HttpMethod method,
        string requestUri,
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string responseBody,
        string? providerErrorCode,
        string? providerErrorMessage,
        IReadOnlyDictionary<string, string> requestIds)
        : base(BuildMessage(serviceName, method, requestUri, statusCode, reasonPhrase, providerErrorCode, providerErrorMessage))
    {
        ServiceName = serviceName;
        Method = method;
        RequestUri = requestUri;
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        ResponseBody = responseBody;
        ProviderErrorCode = providerErrorCode;
        ProviderErrorMessage = providerErrorMessage;
        RequestIds = requestIds;
    }

    public string ServiceName { get; }

    public HttpMethod Method { get; }

    public string RequestUri { get; }

    public HttpStatusCode StatusCode { get; }

    public string? ReasonPhrase { get; }

    public string ResponseBody { get; }

    public string? ProviderErrorCode { get; }

    public string? ProviderErrorMessage { get; }

    public IReadOnlyDictionary<string, string> RequestIds { get; }

    public object ToProblemDetails(bool includeResponseBody)
        => new
        {
            error = "External provider request failed.",
            service = ServiceName,
            statusCode = (int)StatusCode,
            providerStatus = StatusCode.ToString(),
            providerErrorCode = ProviderErrorCode,
            providerErrorMessage = ProviderErrorMessage ?? ReasonPhrase,
            requestUri = RequestUri,
            requestIds = RequestIds,
            responseBody = includeResponseBody ? ResponseBody : null
        };

    private static string BuildMessage(
        string serviceName,
        HttpMethod method,
        string requestUri,
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string? providerErrorCode,
        string? providerErrorMessage)
    {
        var providerMessage = string.IsNullOrWhiteSpace(providerErrorMessage)
            ? reasonPhrase
            : providerErrorMessage;
        var providerCode = string.IsNullOrWhiteSpace(providerErrorCode) ? string.Empty : $" ({providerErrorCode})";
        return $"{serviceName} request {method} {requestUri} failed with {(int)statusCode} {statusCode}{providerCode}: {providerMessage}";
    }
}

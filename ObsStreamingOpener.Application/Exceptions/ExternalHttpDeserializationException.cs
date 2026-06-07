namespace ObsStreamingOpener.Application.Exceptions;

public sealed class ExternalHttpDeserializationException : Exception
{
    public ExternalHttpDeserializationException(
        string serviceName,
        HttpMethod method,
        string requestUri,
        Type responseType,
        string responseBody,
        Exception innerException)
        : base($"{serviceName} request {method} {requestUri} returned JSON that could not be deserialized as {responseType.Name}.", innerException)
    {
        ServiceName = serviceName;
        Method = method;
        RequestUri = requestUri;
        ResponseType = responseType;
        ResponseBody = responseBody;
    }

    public string ServiceName { get; }

    public HttpMethod Method { get; }

    public string RequestUri { get; }

    public Type ResponseType { get; }

    public string ResponseBody { get; }
}

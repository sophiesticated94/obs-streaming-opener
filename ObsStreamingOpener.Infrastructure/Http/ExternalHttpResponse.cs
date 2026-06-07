namespace ObsStreamingOpener.Infrastructure.Http;

public sealed record ExternalHttpResponse<TResponse>(TResponse Body, string RawBody);

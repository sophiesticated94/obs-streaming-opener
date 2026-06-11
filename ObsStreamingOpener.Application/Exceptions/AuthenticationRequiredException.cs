namespace ObsStreamingOpener.Application.Exceptions;

public sealed class AuthenticationRequiredException(string provider, string message) : Exception(message)
{
    public string Provider { get; } = provider;
}

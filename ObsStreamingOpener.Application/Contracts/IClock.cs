namespace ObsStreamingOpener.Application.Contracts;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

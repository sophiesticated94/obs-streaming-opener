using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

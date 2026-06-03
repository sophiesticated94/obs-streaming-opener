using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Infrastructure.Providers;

public sealed class StubTipProviderMonitor(string name, ILogger<StubTipProviderMonitor> logger) : ITipProviderMonitor
{
    public string Name { get; } = name;

    public Task PollAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Provider {ProviderName} is registered as a stub and has no external polling yet.", Name);
        return Task.CompletedTask;
    }
}

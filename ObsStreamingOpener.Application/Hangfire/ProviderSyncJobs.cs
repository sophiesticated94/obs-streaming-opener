using ObsStreamingOpener.Application.Services;

namespace ObsStreamingOpener.Application.Hangfire;

public sealed class ProviderSyncJobs(ProviderMonitorRunner runner)
{
    public Task PollProvidersAsync(CancellationToken cancellationToken = default)
        => runner.PollAllAsync(cancellationToken);
}

using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Application.Hangfire;

public sealed class ProviderSyncJobs(IStreamDataPoller streamDataPoller, IAccountDataPoller accountDataPoller)
{
    public Task PollStreamDataAsync(CancellationToken cancellationToken = default)
        => streamDataPoller.PollAsync(cancellationToken);

    public Task PollAccountDataAsync(CancellationToken cancellationToken = default)
        => accountDataPoller.PollAsync(cancellationToken);
}

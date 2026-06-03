namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderMonitor
{
    string Name { get; }

    Task PollAsync(CancellationToken cancellationToken = default);
}

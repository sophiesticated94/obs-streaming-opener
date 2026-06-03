namespace ObsStreamingOpener.Application.Contracts;

public interface IStreamDataPoller
{
    Task PollAsync(CancellationToken cancellationToken = default);
}

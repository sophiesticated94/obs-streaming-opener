namespace ObsStreamingOpener.Application.Contracts;

public interface IAccountDataPoller
{
    Task PollAsync(CancellationToken cancellationToken = default);
}

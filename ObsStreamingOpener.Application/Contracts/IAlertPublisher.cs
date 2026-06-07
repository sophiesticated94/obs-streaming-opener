using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IAlertPublisher
{
    Task PublishAlertAsync(StreamAlertDto alert, CancellationToken cancellationToken = default);
}

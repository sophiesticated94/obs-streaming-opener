using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStreamEventNotificationHandler
{
    Task HandleAsync(StreamEvent streamEvent, IngestedEventResult result, CancellationToken cancellationToken = default);
}

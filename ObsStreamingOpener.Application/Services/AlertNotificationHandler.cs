using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Application.Services;

public sealed class AlertNotificationHandler(IAlertService alertService) : IStreamEventNotificationHandler
{
    public Task HandleAsync(StreamEvent streamEvent, IngestedEventResult result, CancellationToken cancellationToken = default)
        => alertService.CreateAlertForEventAsync(streamEvent, result, cancellationToken);
}

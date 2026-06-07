using Microsoft.AspNetCore.SignalR;
using ObsStreamingOpener.Api.Hubs;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Api.Services;

public sealed class SignalRAlertPublisher(IHubContext<AlertHub> hubContext) : IAlertPublisher
{
    public async Task PublishAlertAsync(StreamAlertDto alert, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Groups(AlertHubGroups.Channel(alert.MonitoredChannelId), AlertHubGroups.Stream(alert.StreamSessionId))
            .SendAsync("alertCreated", alert, cancellationToken);
    }
}

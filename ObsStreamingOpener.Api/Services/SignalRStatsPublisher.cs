using Microsoft.AspNetCore.SignalR;
using ObsStreamingOpener.Api.Hubs;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Api.Services;

public sealed class SignalRStatsPublisher(IHubContext<StatsHub> hubContext) : IStatsPublisher
{
    public Task PublishCurrentStatsAsync(CurrentStatsDto stats, CancellationToken cancellationToken = default)
        => hubContext.Clients
            .Groups(stats.StreamSessionId.HasValue
                ? [StatsHubGroups.Channel(stats.MonitoredChannelId), StatsHubGroups.Stream(stats.StreamSessionId.Value)]
                : [StatsHubGroups.Channel(stats.MonitoredChannelId)])
            .SendAsync("currentStats", stats, cancellationToken);
}

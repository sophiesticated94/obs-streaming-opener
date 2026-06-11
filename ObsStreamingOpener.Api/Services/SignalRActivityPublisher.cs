using Microsoft.AspNetCore.SignalR;
using ObsStreamingOpener.Api.Hubs;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Api.Services;

public sealed class SignalRActivityPublisher(IHubContext<ActivityHub> hubContext) : IActivityPublisher
{
    public Task PublishEventCreatedAsync(RecentEventDto streamEvent, CancellationToken cancellationToken = default)
        => hubContext.Clients
            .Groups(Groups(streamEvent.MonitoredChannelId, streamEvent.StreamSessionId))
            .SendAsync("eventCreated", streamEvent, cancellationToken);

    public Task PublishMessageCreatedAsync(ProviderMessageDto message, CancellationToken cancellationToken = default)
        => hubContext.Clients
            .Groups(Groups(message.MonitoredChannelId, message.StreamSessionId))
            .SendAsync("messageCreated", message, cancellationToken);

    public Task PublishTipCreatedAsync(TipRealtimeDto tip, CancellationToken cancellationToken = default)
        => hubContext.Clients
            .Groups(Groups(tip.MonitoredChannelId, tip.StreamSessionId))
            .SendAsync("tipCreated", tip, cancellationToken);

    private static IReadOnlyList<string> Groups(Guid channelId, Guid? streamSessionId)
        => streamSessionId.HasValue
            ? [ActivityHubGroups.Channel(channelId), ActivityHubGroups.Stream(streamSessionId.Value)]
            : [ActivityHubGroups.Channel(channelId)];
}

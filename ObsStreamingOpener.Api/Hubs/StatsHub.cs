using Microsoft.AspNetCore.SignalR;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Api.Hubs;

public sealed class StatsHub : Hub
{
    public async Task Subscribe(Guid channelId, Guid? streamSessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, StatsHubGroups.Channel(channelId));
        if (streamSessionId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, StatsHubGroups.Stream(streamSessionId.Value));
        }
    }
}

public static class StatsHubGroups
{
    public static string Channel(Guid channelId) => $"stats:channel:{channelId:N}";
    public static string Stream(Guid streamSessionId) => $"stats:stream:{streamSessionId:N}";
}

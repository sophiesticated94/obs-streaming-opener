using Microsoft.AspNetCore.SignalR;

namespace ObsStreamingOpener.Api.Hubs;

public sealed class ActivityHub : Hub
{
    public async Task Subscribe(Guid channelId, Guid? streamSessionId = null)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ActivityHubGroups.Channel(channelId));
        if (streamSessionId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ActivityHubGroups.Stream(streamSessionId.Value));
        }
    }
}

public static class ActivityHubGroups
{
    public static string Channel(Guid channelId) => $"activity:channel:{channelId:N}";

    public static string Stream(Guid streamSessionId) => $"activity:stream:{streamSessionId:N}";
}

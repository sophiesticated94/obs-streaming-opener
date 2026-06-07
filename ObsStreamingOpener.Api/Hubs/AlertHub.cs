using Microsoft.AspNetCore.SignalR;

namespace ObsStreamingOpener.Api.Hubs;

public sealed class AlertHub : Hub
{
    public async Task Subscribe(Guid channelId, Guid? streamSessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AlertHubGroups.Channel(channelId));
        if (streamSessionId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AlertHubGroups.Stream(streamSessionId.Value));
        }
    }
}

public static class AlertHubGroups
{
    public static string Channel(Guid channelId) => $"channel:{channelId:N}";

    public static string Stream(Guid streamSessionId) => $"stream:{streamSessionId:N}";
}

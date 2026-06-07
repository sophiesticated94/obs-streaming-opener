namespace ObsStreamingOpener.Application.Contracts;

public interface IYouTubeAccountDataMonitor : IAccountProviderMonitor
{
    Task SyncAccountSummaryAsync(CancellationToken cancellationToken = default);

    Task SyncLiveBroadcastsAsync(CancellationToken cancellationToken = default);

    Task SyncContentDiscoveryAsync(CancellationToken cancellationToken = default);

    Task SyncVisibleSubscribersAsync(CancellationToken cancellationToken = default);

    Task SyncSuperChatEventsAsync(CancellationToken cancellationToken = default);

    Task SyncVideoDetailsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default);

    Task SyncVideoCommentsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default);
}

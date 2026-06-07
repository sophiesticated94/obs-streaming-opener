using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Application.Hangfire;

public sealed class ProviderSyncJobs(
    IStreamDataPoller streamDataPoller,
    IAccountDataPoller accountDataPoller,
    IYouTubeAccountDataMonitor youTubeAccountDataMonitor)
{
    public Task PollStreamDataAsync(CancellationToken cancellationToken = default)
        => streamDataPoller.PollAsync(cancellationToken);

    public Task PollAccountDataAsync(CancellationToken cancellationToken = default)
        => accountDataPoller.PollAsync(cancellationToken);

    public Task SyncYouTubeAccountSummaryAsync(CancellationToken cancellationToken = default)
        => youTubeAccountDataMonitor.SyncAccountSummaryAsync(cancellationToken);

    public Task SyncYouTubeLiveBroadcastsAsync(CancellationToken cancellationToken = default)
        => youTubeAccountDataMonitor.SyncLiveBroadcastsAsync(cancellationToken);

    public Task SyncYouTubeContentDiscoveryAsync(CancellationToken cancellationToken = default)
        => youTubeAccountDataMonitor.SyncContentDiscoveryAsync(cancellationToken);

    public Task SyncYouTubeVisibleSubscribersAsync(CancellationToken cancellationToken = default)
        => youTubeAccountDataMonitor.SyncVisibleSubscribersAsync(cancellationToken);

    public Task SyncYouTubeSuperChatEventsAsync(CancellationToken cancellationToken = default)
        => youTubeAccountDataMonitor.SyncSuperChatEventsAsync(cancellationToken);

    public Task SyncYouTubeVideoDetailsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default)
        => youTubeAccountDataMonitor.SyncVideoDetailsAsync(monitoredChannelId, videoId, cancellationToken);

    public Task SyncYouTubeVideoCommentsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default)
        => youTubeAccountDataMonitor.SyncVideoCommentsAsync(monitoredChannelId, videoId, cancellationToken);
}

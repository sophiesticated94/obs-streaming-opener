using Hangfire;

namespace ObsStreamingOpener.Application.Hangfire;

public static class HangfireJobRegistrar
{
    public static void RegisterRecurringJobs(IRecurringJobManager recurringJobs)
    {
        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "stream-data-sync",
            job => job.PollStreamDataAsync(CancellationToken.None),
            Cron.Minutely);

        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "account-data-sync",
            job => job.PollAccountDataAsync(CancellationToken.None),
            Cron.Minutely);

        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "youtube-account-summary-sync",
            job => job.SyncYouTubeAccountSummaryAsync(CancellationToken.None),
            "*/5 * * * *");

        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "youtube-live-broadcast-sync",
            job => job.SyncYouTubeLiveBroadcastsAsync(CancellationToken.None),
            Cron.Minutely);

        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "youtube-content-discovery-sync",
            job => job.SyncYouTubeContentDiscoveryAsync(CancellationToken.None),
            "*/15 * * * *");

        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "youtube-subscriber-sync",
            job => job.SyncYouTubeVisibleSubscribersAsync(CancellationToken.None),
            "*/30 * * * *");

        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "youtube-super-chat-sync",
            job => job.SyncYouTubeSuperChatEventsAsync(CancellationToken.None),
            "*/10 * * * *");
    }
}

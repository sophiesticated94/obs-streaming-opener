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
    }
}

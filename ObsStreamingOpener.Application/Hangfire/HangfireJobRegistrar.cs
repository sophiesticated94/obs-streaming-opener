using Hangfire;

namespace ObsStreamingOpener.Application.Hangfire;

public static class HangfireJobRegistrar
{
    public static void RegisterRecurringJobs(IRecurringJobManager recurringJobs)
    {
        recurringJobs.AddOrUpdate<ProviderSyncJobs>(
            "provider-sync",
            job => job.PollProvidersAsync(CancellationToken.None),
            Cron.Minutely);
    }
}

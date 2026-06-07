using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Application.Services;

public sealed class AccountDataPoller(
    IEnumerable<IAccountProviderMonitor> accountMonitors,
    ILogger<AccountDataPoller> logger) : IAccountDataPoller
{
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        foreach (var monitor in accountMonitors)
        {
            try
            {
                await monitor.PollAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Account data monitor {ProviderMonitor} failed.", monitor.Name);
            }
        }
    }
}

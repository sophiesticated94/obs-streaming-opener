using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Application.Services;

public sealed class ProviderMonitorRunner(
    IEnumerable<IProviderMonitor> monitors,
    ILogger<ProviderMonitorRunner> logger)
{
    public async Task PollAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var monitor in monitors)
        {
            try
            {
                await monitor.PollAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Provider monitor {ProviderMonitor} failed.", monitor.Name);
            }
        }
    }
}

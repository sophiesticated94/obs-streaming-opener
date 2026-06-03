using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Options;

namespace ObsStreamingOpener.Application.HostedServices;

public sealed class StreamDataPollingService(
    IServiceScopeFactory scopeFactory,
    IOptions<StreamingMonitorOptions> options,
    ILogger<StreamDataPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.EnableStreamDataPolling)
        {
            logger.LogInformation("High-frequency stream data polling is disabled.");
            return;
        }

        var intervalSeconds = Math.Max(5, settings.StreamDataPollingSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollStreamDataAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stream data polling failed.");
            }
        }
    }

    private async Task PollStreamDataAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IStreamDataPoller>().PollAsync(cancellationToken);
    }
}

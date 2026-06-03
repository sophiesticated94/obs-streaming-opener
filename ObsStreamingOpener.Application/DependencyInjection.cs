using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Hangfire;
using ObsStreamingOpener.Application.HostedServices;
using ObsStreamingOpener.Application.Options;
using ObsStreamingOpener.Application.Services;

namespace ObsStreamingOpener.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StreamingMonitorOptions>(configuration.GetSection("StreamingMonitor"));
        services.AddScoped<IEventIngestionService, EventIngestionService>();
        services.AddScoped<IStatsQueryService, StatsQueryService>();
        services.AddScoped<ProviderMonitorRunner>();
        services.AddScoped<ProviderSyncJobs>();
        services.AddHostedService<YouTubeMetricPollingService>();

        return services;
    }
}

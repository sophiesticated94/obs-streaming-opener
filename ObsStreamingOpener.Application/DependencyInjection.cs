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
        services.Configure<YouTubeOAuthOptions>(configuration.GetSection("YouTubeOAuth"));
        services.AddScoped<ICredentialProtector, DataProtectionCredentialProtector>();
        services.AddSingleton<IProviderEventIdentityService, ProviderEventIdentityService>();
        services.AddScoped<IEventIngestionService, EventIngestionService>();
        services.AddScoped<IStatsPublisher, NoOpStatsPublisher>();
        services.AddScoped<IActivityPublisher, NoOpActivityPublisher>();
        services.AddScoped<IAudienceIngestionService, AudienceIngestionService>();
        services.AddScoped<ISupportIngestionService, SupportIngestionService>();
        services.AddScoped<IStatsQueryService, StatsQueryService>();
        services.AddScoped<IContentQueryService, ContentQueryService>();
        services.AddScoped<IFeeEstimator, NoOpFeeEstimator>();
        services.AddScoped<IRevenueCalculator, RevenueCalculator>();
        services.AddScoped<IRevenueForecastService, RevenueForecastService>();
        services.AddScoped<IRevenueSynchronizer, RevenueSynchronizer>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IStreamEventNotificationHandler, AlertNotificationHandler>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IYouTubeOAuthService, YouTubeOAuthService>();
        services.AddScoped<IYouTubeCredentialResolver, YouTubeCredentialResolver>();
        services.AddScoped<IYouTubeAccountDataMonitor, YouTubeAccountDataMonitor>();
        services.AddScoped<IAccountProviderMonitor>(sp => sp.GetRequiredService<IYouTubeAccountDataMonitor>());
        services.AddScoped<IStreamDataPoller, StreamDataPoller>();
        services.AddScoped<IAccountDataPoller, AccountDataPoller>();
        services.AddScoped<ProviderSyncJobs>();
        services.AddHostedService<StreamDataPollingService>();

        return services;
    }
}

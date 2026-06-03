using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Options;
using ObsStreamingOpener.Infrastructure.Providers;
using ObsStreamingOpener.Infrastructure.Time;
using ObsStreamingOpener.Infrastructure.YouTube;

namespace ObsStreamingOpener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<YouTubeOptions>(configuration.GetSection("YouTube"));
        services.AddSingleton<IClock, SystemClock>();
        services.AddHttpClient<IYouTubeApiClient, YouTubeApiClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<YouTubeOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });

        services.AddScoped<YouTubeLiveChatMonitor>();
        services.AddScoped<IStreamingProviderMonitor>(sp => sp.GetRequiredService<YouTubeLiveChatMonitor>());
        services.AddScoped<IProviderMonitor>(sp => sp.GetRequiredService<YouTubeLiveChatMonitor>());

        services.AddScoped<IProviderMonitor>(sp => new StubTipProviderMonitor(ProviderKind.Tipply.ToString(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StubTipProviderMonitor>>()));
        services.AddScoped<IProviderMonitor>(sp => new StubTipProviderMonitor(ProviderKind.Patronite.ToString(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<StubTipProviderMonitor>>()));

        return services;
    }
}

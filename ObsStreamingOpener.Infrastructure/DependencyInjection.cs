using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Http;
using ObsStreamingOpener.Infrastructure.Browser;
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
        services.Configure<BrowserAutomationOptions>(configuration.GetSection("BrowserAutomation"));
        services.Configure<SupportProviderOptions>(ProviderKind.Tipply.ToString(), configuration.GetSection("SupportProviders:Tipply"));
        services.Configure<SupportProviderOptions>(ProviderKind.Patronite.ToString(), configuration.GetSection("SupportProviders:Patronite"));
        services.Configure<SupportProviderOptions>(ProviderKind.Zrzutka.ToString(), configuration.GetSection("SupportProviders:Zrzutka"));
        services.AddMemoryCache();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ILoginStateService, LoginStateService>();
        services.AddScoped<IBrowserSessionFactory, BrowserSessionFactory>();
        services.AddHttpClient<IExternalHttpClient, ExternalHttpClient>();
        services.AddScoped<IYouTubeApiClient, YouTubeApiClient>();
        services.AddScoped<IYouTubeOAuthClient, YouTubeOAuthClient>();
        services.AddScoped<IStreamProviderDataProvider, YouTubeStreamProviderDataProvider>();
        services.Decorate<IStreamProviderDataProvider, CachedStreamProviderDataProvider>();

        services.AddScoped<YouTubeLiveChatMonitor>();
        services.AddScoped<IStreamingProviderMonitor>(sp => sp.GetRequiredService<YouTubeLiveChatMonitor>());
        services.AddScoped<IProviderMonitor>(sp => sp.GetRequiredService<YouTubeLiveChatMonitor>());

        services.AddScoped<ISupportProviderAdapter>(sp => new EmptySupportProviderAdapter(ProviderKind.Tipply, sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SupportProviderOptions>>()));
        services.AddScoped<ISupportProviderAdapter>(sp => new EmptySupportProviderAdapter(ProviderKind.Patronite, sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SupportProviderOptions>>()));
        services.AddScoped<ISupportProviderAdapter>(sp => new EmptySupportProviderAdapter(ProviderKind.Zrzutka, sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SupportProviderOptions>>()));

        return services;
    }
}

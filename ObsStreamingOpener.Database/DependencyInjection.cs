using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Database;

public static class DependencyInjection
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("StreamingOpener")
            ?? "Data Source=data/streaming-opener.db";

        services.AddDbContext<StreamingOpenerDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<StreamingOpenerRepository>();
        services.AddScoped<IChannelStore>(sp => sp.GetRequiredService<StreamingOpenerRepository>());
        services.AddScoped<IEventStore>(sp => sp.GetRequiredService<StreamingOpenerRepository>());
        services.AddScoped<IStatsStore>(sp => sp.GetRequiredService<StreamingOpenerRepository>());
        services.AddScoped<IProviderCursorStore>(sp => sp.GetRequiredService<StreamingOpenerRepository>());
        services.AddScoped<IStreamSessionStore>(sp => sp.GetRequiredService<StreamingOpenerRepository>());
        services.AddScoped<IAudienceStore>(sp => sp.GetRequiredService<StreamingOpenerRepository>());

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ObsStreamingOpener.Application;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Hangfire;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Database;
using ObsStreamingOpener.Infrastructure;

namespace ObsStreamingOpener.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void LogicalServices_AreResolvableFromDependencyInjection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StreamingMonitor:EnableStreamDataPolling"] = "false",
                ["YouTube:BaseUrl"] = "https://www.googleapis.com/youtube/v3/"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddApplication(configuration);
        services.AddInfrastructure(configuration);
        services.AddDatabase(configuration);
        services.RemoveAll<DbContextOptions<StreamingOpenerDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<StreamingOpenerDbContext>>();
        services.AddDbContext<StreamingOpenerDbContext>(options => options.UseInMemoryDatabase($"di-tests-{Guid.NewGuid():N}"));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEventIngestionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAudienceIngestionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStatsQueryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStreamDataPoller>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAccountDataPoller>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IChannelStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAudienceStore>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProviderSyncJobs>());
        Assert.NotEmpty(scope.ServiceProvider.GetServices<IProviderMonitor>());
    }
}

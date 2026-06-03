using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Database;

namespace ObsStreamingOpener.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"obs-streaming-opener-tests-{Guid.NewGuid():N}";
    private readonly string _sqliteDirectory = Path.Combine(Path.GetTempPath(), $"obs-streaming-opener-sqlite-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_sqliteDirectory);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:StreamingOpener"] = $"Data Source={Path.Combine(_sqliteDirectory, "streaming-opener.db")}",
                ["StreamingMonitor:EnableStreamDataPolling"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<StreamingOpenerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<StreamingOpenerDbContext>>();
            services.AddDbContext<StreamingOpenerDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task SeedDatabaseAsync(Func<IIntegrationTestDataSeeder, Task> seed)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StreamingOpenerDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var seeder = new IntegrationTestDataSeeder(
            dbContext,
            scope.ServiceProvider.GetRequiredService<IClock>());

        await seed(seeder);
    }
}

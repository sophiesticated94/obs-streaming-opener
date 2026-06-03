using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ObsStreamingOpener.Database;

namespace ObsStreamingOpener.Tests;

public sealed class ApiSmokeTests
{
    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"obs-streaming-opener-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:StreamingOpener"] = $"Data Source={Path.Combine(tempDirectory, "streaming-opener.db")}",
                        ["ConnectionStrings:Hangfire"] = $"Data Source={Path.Combine(tempDirectory, "hangfire.db")}",
                        ["StreamingMonitor:EnableYouTubePolling"] = "false"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<StreamingOpenerDbContext>>();
                    services.AddSingleton(_ =>
                    {
                        var connection = new SqliteConnection("Data Source=:memory:");
                        connection.Open();
                        return connection;
                    });
                    services.AddDbContext<StreamingOpenerDbContext>((sp, options) =>
                    {
                        options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
                    });
                });
            });

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}

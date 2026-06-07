using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
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
                ["StreamingMonitor:EnableStreamDataPolling"] = "false",
                ["YouTubeOAuth:ClientId"] = "test-client-id",
                ["YouTubeOAuth:ClientSecret"] = "test-client-secret",
                ["YouTubeOAuth:RedirectUri"] = "http://localhost/api/auth/youtube/callback"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<StreamingOpenerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<StreamingOpenerDbContext>>();
            services.RemoveAll<IYouTubeOAuthClient>();
            services.AddDbContext<StreamingOpenerDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddSingleton<IYouTubeOAuthClient, FakeYouTubeOAuthClient>();
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

    private sealed class FakeYouTubeOAuthClient : IYouTubeOAuthClient
    {
        public Task<YouTubeTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeTokenResponse(
                $"access-token-for-{code}",
                $"refresh-token-for-{code}",
                3600,
                "Bearer",
                "openid email profile https://www.googleapis.com/auth/youtube.readonly"));

        public Task<YouTubeTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeTokenResponse(
                $"refreshed-access-token-for-{refreshToken}",
                null,
                7200,
                "Bearer",
                "openid email profile https://www.googleapis.com/auth/youtube.readonly"));

        public Task<YouTubeUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeUserInfo(
                "google-user-1",
                "creator@example.test",
                "Creator From OAuth"));

        public Task<IReadOnlyList<YouTubeChannelInfo>> GetMyChannelsAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<YouTubeChannelInfo>>(
            [
                new YouTubeChannelInfo(
                    "youtube-channel-1",
                    "OAuth Channel",
                    "https://youtube.com/channel/youtube-channel-1",
                    1234,
                    5678,
                    "{\"id\":\"youtube-channel-1\"}")
            ]);
    }
}

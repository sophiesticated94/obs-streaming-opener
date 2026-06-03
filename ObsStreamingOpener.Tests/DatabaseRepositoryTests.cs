using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Database;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public sealed class DatabaseRepositoryTests
{
    [Fact]
    public async Task IngestAsync_StoresEventAndSkipsDuplicateExternalEventId()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var ingestion = new EventIngestionService(fixture.Repository, fixture.Clock);
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync();
        var providerEvent = new ProviderEvent(
            session.Id,
            ProviderKind.YouTube,
            StreamEventType.ChatMessage,
            "yt-message-1",
            "Viewer",
            "viewer-1",
            "Chat",
            "Hello stream",
            null,
            null,
            fixture.Clock.UtcNow,
            "{}");

        var first = await ingestion.IngestAsync(providerEvent);
        var second = await ingestion.IngestAsync(providerEvent);
        var recent = await fixture.Repository.GetRecentEventsAsync(null, null, 10);

        Assert.True(first.Stored);
        Assert.False(first.Duplicate);
        Assert.False(second.Stored);
        Assert.True(second.Duplicate);
        Assert.Single(recent);
    }

    [Fact]
    public async Task SetCursorAsync_UpsertsCursorValue()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync();
        var connection = new ProviderConnection
        {
            StreamSessionId = session.Id,
            Provider = ProviderKind.YouTube,
            ExternalChannelId = "live-chat-id",
            ExternalStreamId = "video-id",
            DisplayName = "YouTube"
        };

        fixture.DbContext.ProviderConnections.Add(connection);
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Repository.SetCursorAsync(connection.Id, "page", "one");
        await fixture.Repository.SetCursorAsync(connection.Id, "page", "two");

        Assert.Equal("two", await fixture.Repository.GetCursorAsync(connection.Id, "page"));
    }

    [Fact]
    public async Task StatsQueryService_CalculatesCurrentStatsFromLatestMetrics()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync();
        fixture.DbContext.MetricSnapshots.AddRange(
            new MetricSnapshot
            {
                StreamSessionId = session.Id,
                Provider = ProviderKind.YouTube,
                Metric = MetricKind.ConcurrentViewers,
                Value = 42,
                CapturedAt = fixture.Clock.UtcNow.AddMinutes(-1)
            },
            new MetricSnapshot
            {
                StreamSessionId = session.Id,
                Provider = ProviderKind.YouTube,
                Metric = MetricKind.ConcurrentViewers,
                Value = 84,
                CapturedAt = fixture.Clock.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var service = new StatsQueryService(fixture.Repository, fixture.Repository, fixture.Clock);
        var stats = await service.GetCurrentStatsAsync();

        Assert.Equal(84, stats.ConcurrentViewers);
        Assert.Equal(session.Id, stats.StreamSessionId);
    }

    private sealed class RepositoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RepositoryFixture(SqliteConnection connection, StreamingOpenerDbContext dbContext, TestClock clock)
        {
            _connection = connection;
            DbContext = dbContext;
            Clock = clock;
            Repository = new StreamingOpenerRepository(dbContext, clock);
        }

        public StreamingOpenerDbContext DbContext { get; }

        public TestClock Clock { get; }

        public StreamingOpenerRepository Repository { get; }

        public static async Task<RepositoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<StreamingOpenerDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new StreamingOpenerDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new RepositoryFixture(connection, dbContext, new TestClock());
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    }
}

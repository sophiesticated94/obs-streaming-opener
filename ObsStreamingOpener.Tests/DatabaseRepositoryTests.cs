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
    public async Task IngestAsync_StoresEventOutsideStreamWithChannelAndNullStreamSession()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var ingestion = new EventIngestionService(fixture.Repository, fixture.Clock);

        var result = await ingestion.IngestAsync(new ProviderEvent(
            channel.Id,
            null,
            null,
            ProviderKind.YouTube,
            StreamEventType.ChatMessage,
            "yt-message-1",
            "Viewer",
            "viewer-1",
            "Chat",
            "Hello between streams",
            null,
            null,
            fixture.Clock.UtcNow,
            "{}"));

        var recent = await fixture.Repository.GetRecentEventsAsync(channel.Id, null, null, 10);

        Assert.True(result.Stored);
        var stored = Assert.Single(recent);
        Assert.Equal(channel.Id, stored.MonitoredChannelId);
        Assert.Null(stored.StreamSessionId);
    }

    [Fact]
    public async Task IngestAsync_StoresEventDuringStreamWithChannelAndStreamSession()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync(channel.Id);
        var ingestion = new EventIngestionService(fixture.Repository, fixture.Clock);

        await ingestion.IngestAsync(new ProviderEvent(
            channel.Id,
            session.Id,
            null,
            ProviderKind.YouTube,
            StreamEventType.ChatMessage,
            "yt-message-stream-1",
            "Viewer",
            "viewer-1",
            "Chat",
            "Hello live",
            null,
            null,
            fixture.Clock.UtcNow,
            "{}"));

        var stored = Assert.Single(await fixture.Repository.GetRecentEventsAsync(channel.Id, null, null, 10));
        Assert.Equal(session.Id, stored.StreamSessionId);
    }

    [Fact]
    public async Task IngestAsync_SkipsDuplicateProviderEventWithinChannel()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var ingestion = new EventIngestionService(fixture.Repository, fixture.Clock);
        var providerEvent = new ProviderEvent(
            channel.Id,
            null,
            null,
            ProviderKind.YouTube,
            StreamEventType.ChatMessage,
            "yt-message-duplicate",
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

        Assert.True(first.Stored);
        Assert.True(second.Duplicate);
        Assert.Single(await fixture.Repository.GetRecentEventsAsync(channel.Id, null, null, 10));
    }

    [Fact]
    public async Task SetCursorAsync_UpsertsCursorValueForProviderConnection()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var connection = new ProviderConnection
        {
            MonitoredChannelId = channel.Id,
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
    public async Task StatsQueryService_CalculatesCurrentStatsForChannel()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        fixture.DbContext.MetricSnapshots.AddRange(
            new MetricSnapshot
            {
                MonitoredChannelId = channel.Id,
                Provider = ProviderKind.YouTube,
                Metric = MetricKind.ConcurrentViewers,
                SnapshotReason = SnapshotReason.ScheduledPoll,
                Value = 42,
                CapturedAt = fixture.Clock.UtcNow.AddMinutes(-1)
            },
            new MetricSnapshot
            {
                MonitoredChannelId = channel.Id,
                Provider = ProviderKind.YouTube,
                Metric = MetricKind.AudienceMemberCount,
                SnapshotReason = SnapshotReason.StreamStarted,
                Value = 1000,
                CapturedAt = fixture.Clock.UtcNow
            });
        await fixture.DbContext.SaveChangesAsync();

        var service = new StatsQueryService(fixture.Repository, fixture.Repository, fixture.Repository, fixture.Clock);
        var stats = await service.GetCurrentStatsAsync(channel.Id);

        Assert.Equal(channel.Id, stats.MonitoredChannelId);
        Assert.Equal(42, stats.ConcurrentViewers);
        Assert.Equal(1000, stats.AudienceMemberCount);
    }

    [Fact]
    public async Task AddMetricSnapshotIfChangedAsync_SkipsUnchangedValueForSameMetricScope()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var connection = new ProviderConnection
        {
            MonitoredChannelId = channel.Id,
            Provider = ProviderKind.YouTube,
            ExternalChannelId = "live-chat-id",
            ExternalStreamId = "video-id",
            DisplayName = "YouTube"
        };
        fixture.DbContext.ProviderConnections.Add(connection);
        await fixture.DbContext.SaveChangesAsync();

        var first = await fixture.Repository.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
        {
            MonitoredChannelId = channel.Id,
            ProviderConnectionId = connection.Id,
            Provider = ProviderKind.YouTube,
            Metric = MetricKind.ConcurrentViewers,
            SnapshotReason = SnapshotReason.ScheduledPoll,
            Value = 42,
            Unit = "viewers",
            CapturedAt = fixture.Clock.UtcNow
        });
        var second = await fixture.Repository.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
        {
            MonitoredChannelId = channel.Id,
            ProviderConnectionId = connection.Id,
            Provider = ProviderKind.YouTube,
            Metric = MetricKind.ConcurrentViewers,
            SnapshotReason = SnapshotReason.ScheduledPoll,
            Value = 42,
            Unit = "viewers",
            CapturedAt = fixture.Clock.UtcNow.AddSeconds(5)
        });

        Assert.True(first);
        Assert.False(second);
        Assert.Single(await fixture.Repository.GetMetricsAsync(channel.Id, fixture.Clock.UtcNow.AddMinutes(-1), fixture.Clock.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public async Task AddMetricSnapshotIfChangedAsync_StoresChangedValueForSameMetricScope()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();

        await fixture.Repository.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
        {
            MonitoredChannelId = channel.Id,
            Provider = ProviderKind.YouTube,
            Metric = MetricKind.AudienceMemberCount,
            SnapshotReason = SnapshotReason.ScheduledPoll,
            Value = 100,
            Unit = "members",
            CapturedAt = fixture.Clock.UtcNow
        });
        var changed = await fixture.Repository.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
        {
            MonitoredChannelId = channel.Id,
            Provider = ProviderKind.YouTube,
            Metric = MetricKind.AudienceMemberCount,
            SnapshotReason = SnapshotReason.ScheduledPoll,
            Value = 101,
            Unit = "members",
            CapturedAt = fixture.Clock.UtcNow.AddSeconds(5)
        });

        Assert.True(changed);
        Assert.Equal(2, (await fixture.Repository.GetMetricsAsync(channel.Id, fixture.Clock.UtcNow.AddMinutes(-1), fixture.Clock.UtcNow.AddMinutes(1))).Count);
    }

    [Fact]
    public async Task AudienceIngestion_CreatesRelationshipAndDetectsRenewal()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var eventIngestion = new EventIngestionService(fixture.Repository, fixture.Clock);
        var audienceIngestion = new AudienceIngestionService(fixture.Repository, eventIngestion, fixture.Clock);

        var first = await audienceIngestion.IngestRelationshipAsync(new ProviderAudienceRelationship(
            channel.Id,
            ProviderKind.YouTube,
            "audience-1",
            "Audience One",
            null,
            AudienceRelationshipKind.Free,
            fixture.Clock.UtcNow,
            false,
            "{}"));

        var latest = await fixture.Repository.GetLatestRelationshipPeriodAsync(channel.Id, first.AudienceMemberId, AudienceRelationshipKind.Free);
        fixture.DbContext.ChangeTracker.Clear();
        latest!.EndedAt = fixture.Clock.UtcNow.AddDays(1);
        fixture.DbContext.AudienceRelationshipPeriods.Update(latest);
        await fixture.DbContext.SaveChangesAsync();

        var second = await audienceIngestion.IngestRelationshipAsync(new ProviderAudienceRelationship(
            channel.Id,
            ProviderKind.YouTube,
            "audience-1",
            "Audience One",
            null,
            AudienceRelationshipKind.Free,
            fixture.Clock.UtcNow.AddDays(2),
            false,
            "{}"));

        Assert.True(first.CreatedAudienceMember);
        Assert.True(first.CreatedRelationshipPeriod);
        Assert.True(second.Renewed);
        Assert.Equal(2, (await fixture.Repository.GetRelationshipHistoryAsync(channel.Id, first.AudienceMemberId)).Count);
    }

    private sealed class RepositoryFixture : IAsyncDisposable
    {
        private RepositoryFixture(StreamingOpenerDbContext dbContext, TestClock clock)
        {
            DbContext = dbContext;
            Clock = clock;
            Repository = new StreamingOpenerRepository(dbContext, clock);
        }

        public StreamingOpenerDbContext DbContext { get; }

        public TestClock Clock { get; }

        public StreamingOpenerRepository Repository { get; }

        public static async Task<RepositoryFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<StreamingOpenerDbContext>()
                .UseInMemoryDatabase($"repo-tests-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new StreamingOpenerDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            return new RepositoryFixture(dbContext, new TestClock());
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    }
}

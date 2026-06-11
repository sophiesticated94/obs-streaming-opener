using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Database;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.YouTube;

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
    public async Task IngestAsync_StoresTipInDedicatedTipTableAndEventUsesGenericValue()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync(channel.Id);
        var ingestion = new EventIngestionService(fixture.Repository, fixture.Clock);

        var result = await ingestion.IngestAsync(new ProviderEvent(
            channel.Id,
            session.Id,
            null,
            null,
            ProviderKind.Custom,
            StreamEventType.Tip,
            "tip-table-1",
            "Tipper",
            "tipper-1",
            "Tip",
            "Keep going",
            42,
            "PLN",
            fixture.Clock.UtcNow,
            "{}"));

        var streamEvent = await fixture.DbContext.StreamEvents.SingleAsync(x => x.Id == result.EventId);
        var tip = await fixture.DbContext.Tips.SingleAsync(x => x.StreamEventId == streamEvent.Id);

        Assert.Equal(42, streamEvent.Value);
        Assert.Equal("PLN", streamEvent.Unit);
        Assert.Equal(42, tip.Amount);
        Assert.Equal("PLN", tip.Currency);
        Assert.Equal(streamEvent.Id, tip.StreamEventId);
        Assert.Equal(session.Id, tip.StreamSessionId);
    }

    [Fact]
    public async Task YouTubeLiveChatMonitor_StoresSuperChatAsTipForCurrentStream()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync(channel.Id);
        fixture.DbContext.ProviderConnections.Add(new ProviderConnection
        {
            MonitoredChannelId = channel.Id,
            Provider = ProviderKind.YouTube,
            ExternalChannelId = channel.ExternalChannelId,
            ExternalStreamId = "video-1",
            IsEnabled = true
        });
        await fixture.DbContext.SaveChangesAsync();

        var ingestion = new EventIngestionService(fixture.Repository, fixture.Clock);
        var monitor = new YouTubeLiveChatMonitor(
            fixture.Repository,
            fixture.Repository,
            fixture.Repository,
            ingestion,
            new AudienceIngestionService(fixture.Repository, ingestion, fixture.Clock),
            fixture.Repository,
            new ProviderEventIdentityService(),
            new FakeLiveChatYouTubeApiClient(),
            new FakeYouTubeCredentialResolver(),
            NullLogger<YouTubeLiveChatMonitor>.Instance);

        await monitor.PollAsync();

        var streamEvent = await fixture.DbContext.StreamEvents.SingleAsync(x => x.EventType == StreamEventType.Tip);
        var tip = await fixture.DbContext.Tips.SingleAsync(x => x.StreamEventId == streamEvent.Id);
        var message = await fixture.DbContext.ProviderMessages.SingleAsync(x => x.ExternalMessageId == "super-chat-1");
        var tipTotal = await fixture.Repository.GetLatestMetricAsync(channel.Id, MetricKind.TipTotal);

        Assert.Equal(session.Id, streamEvent.StreamSessionId);
        Assert.Equal(12.34m, streamEvent.Value);
        Assert.Equal("PLN", streamEvent.Unit);
        Assert.Equal(12.34m, tip.Amount);
        Assert.Equal("PLN", tip.Currency);
        Assert.Equal(12.34m, message.Amount);
        Assert.Equal(12.34m, tipTotal!.Value);
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

        var service = new StatsQueryService(fixture.Repository, fixture.Repository, fixture.Repository, fixture.Repository, fixture.Clock);
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

    [Fact]
    public async Task UpsertEventAsync_UpdatesExistingEventByIdentityKeyInsteadOfDuplicating()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var identityKey = "YouTube:ContentPublished:native:youtube-content:video-1";

        await fixture.Repository.UpsertEventAsync(new StreamEvent
        {
            MonitoredChannelId = channel.Id,
            Provider = ProviderKind.YouTube,
            EventType = StreamEventType.ContentPublished,
            ExternalEventId = "youtube-content:video-1",
            IdentityKey = identityKey,
            PayloadHash = "one",
            Title = "Old title",
            OccurredAt = fixture.Clock.UtcNow,
            StoredAt = fixture.Clock.UtcNow,
            LastSeenAt = fixture.Clock.UtcNow
        });

        var result = await fixture.Repository.UpsertEventAsync(new StreamEvent
        {
            MonitoredChannelId = channel.Id,
            Provider = ProviderKind.YouTube,
            EventType = StreamEventType.ContentPublished,
            ExternalEventId = "youtube-content:video-1",
            IdentityKey = identityKey,
            PayloadHash = "two",
            Title = "New title",
            OccurredAt = fixture.Clock.UtcNow.AddMinutes(1),
            StoredAt = fixture.Clock.UtcNow,
            LastSeenAt = fixture.Clock.UtcNow
        });

        var stored = Assert.Single(await fixture.Repository.GetRecentEventsAsync(channel.Id, null, null, 10));
        Assert.True(result.Stored);
        Assert.False(result.Duplicate);
        Assert.Equal("New title", stored.Title);
    }

    [Fact]
    public async Task UpsertMessageAsync_StoresAndUpdatesProviderMessage()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var message = new ProviderMessageUpsert(
            channel.Id,
            null,
            null,
            ProviderKind.YouTube,
            MessageSource.LiveChat,
            "chat-1",
            "YouTube:LiveChat:native:chat-1",
            "author-1",
            "Viewer",
            null,
            "Hello",
            fixture.Clock.UtcNow,
            null,
            false,
            false,
            false,
            false,
            null,
            null,
            "{}");

        await fixture.Repository.UpsertMessageAsync(message);
        await fixture.Repository.UpsertMessageAsync(message with { MessageText = "Updated" });

        var stored = Assert.Single(await fixture.Repository.GetRecentMessagesAsync(channel.Id, MessageSource.LiveChat, 10));
        Assert.Equal("Updated", stored.MessageText);
        Assert.Equal("Viewer", stored.AuthorDisplayName);
    }

    [Fact]
    public async Task AlertService_CreatesAlertForStreamEventAndSkipsOutsideStreamEvent()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync(channel.Id);
        var alertService = new AlertService(fixture.Repository, fixture.Repository, fixture.Repository, fixture.Clock);
        var ingestion = new EventIngestionService(
            fixture.Repository,
            fixture.Clock,
            notificationHandlers: [new AlertNotificationHandler(alertService)]);

        await ingestion.IngestAsync(new ProviderEvent(
            channel.Id,
            session.Id,
            null,
            null,
            ProviderKind.Custom,
            StreamEventType.Tip,
            "tip-with-session",
            "Tipper",
            null,
            "Tip",
            "Nice stream",
            20,
            "PLN",
            fixture.Clock.UtcNow,
            "{}"));
        await ingestion.IngestAsync(new ProviderEvent(
            channel.Id,
            null,
            null,
            null,
            ProviderKind.Custom,
            StreamEventType.Tip,
            "tip-outside-session",
            "Tipper",
            null,
            "Tip",
            "No stream",
            20,
            "PLN",
            fixture.Clock.UtcNow,
            "{}"));

        var alert = Assert.Single(await fixture.Repository.GetRecentAlertsAsync(channel.Id, session.Id, 10));
        Assert.Equal(session.Id, alert.StreamSessionId);
        Assert.False(alert.IsSystemAlert);
        Assert.NotNull(alert.StreamEventId);
    }

    [Fact]
    public async Task AlertService_CreatesManualSystemAlertForCurrentSession()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var session = await fixture.Repository.GetOrCreateCurrentSessionAsync(channel.Id);
        var alertService = new AlertService(fixture.Repository, fixture.Repository, fixture.Repository, fixture.Clock);

        var alert = await alertService.CreateManualAlertAsync(channel.Id, new ManualAlertRequest(
            null,
            "Manual",
            "Preview",
            "fireworks",
            7,
            null,
            null));

        Assert.Equal(session.Id, alert.StreamSessionId);
        Assert.True(alert.IsSystemAlert);
        Assert.Null(alert.StreamEventId);
    }

    [Fact]
    public async Task UpsertResource_MergesVideoAndLiveBroadcastWithPatchHistory()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();

        var video = await fixture.Repository.UpsertResourceAsync(new ProviderResourceUpsert(
            channel.Id,
            ProviderKind.YouTube,
            ProviderResourceKind.Video,
            "video-123",
            "First title",
            null,
            "https://youtube.test/watch?v=video-123",
            "https://img.youtube.test/video-123.jpg",
            "public",
            fixture.Clock.UtcNow.AddDays(-1),
            null,
            null,
            null,
            3600,
            "{}"));

        var broadcast = await fixture.Repository.UpsertResourceAsync(new ProviderResourceUpsert(
            channel.Id,
            ProviderKind.YouTube,
            ProviderResourceKind.LiveBroadcast,
            "video-123",
            "Updated title",
            null,
            "https://youtube.test/watch?v=video-123",
            "https://img.youtube.test/video-123-updated.jpg",
            "complete",
            fixture.Clock.UtcNow.AddDays(-1),
            fixture.Clock.UtcNow.AddHours(-2),
            fixture.Clock.UtcNow.AddHours(-1),
            fixture.Clock.UtcNow,
            3700,
            "{}"));

        Assert.Equal(video.Id, broadcast.Id);
        Assert.Equal(ProviderResourceKind.LiveBroadcast, broadcast.ResourceKind);
        Assert.Contains(ProviderResourceKind.Video, broadcast.ObservedKinds);
        Assert.Contains(ProviderResourceKind.LiveBroadcast, broadcast.ObservedKinds);
        Assert.Contains(broadcast.PatchHistory.SelectMany(x => x.Fields), x => x.Field == nameof(ProviderResource.Title));
        Assert.Contains(broadcast.PatchHistory.SelectMany(x => x.Fields), x => x.Field == nameof(ProviderResource.ThumbnailUrl));
        Assert.Contains(broadcast.PatchHistory.SelectMany(x => x.Fields), x => x.Field == nameof(ProviderResource.DurationSeconds));
        Assert.Single(await fixture.DbContext.ProviderResources.Where(x => x.ExternalResourceId == "video-123").ToListAsync());
    }

    [Fact]
    public async Task UpsertResource_UnchangedResourceDoesNotAppendPatch()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var upsert = new ProviderResourceUpsert(
            channel.Id,
            ProviderKind.YouTube,
            ProviderResourceKind.Video,
            "stable-video",
            "Stable title",
            null,
            null,
            null,
            "public",
            fixture.Clock.UtcNow,
            null,
            null,
            null,
            null,
            "{}");

        var first = await fixture.Repository.UpsertResourceAsync(upsert);
        var second = await fixture.Repository.UpsertResourceAsync(upsert);

        Assert.Equal(first.PatchHistory.Count, second.PatchHistory.Count);
    }

    [Fact]
    public async Task UpsertResource_CompactsExistingDuplicatePatchHistory()
    {
        await using var fixture = await RepositoryFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var upsert = new ProviderResourceUpsert(
            channel.Id,
            ProviderKind.YouTube,
            ProviderResourceKind.Video,
            "duplicate-history-video",
            " Stable title ",
            null,
            null,
            null,
            "public",
            fixture.Clock.UtcNow,
            null,
            null,
            null,
            null,
            "{}");

        var first = await fixture.Repository.UpsertResourceAsync(upsert);
        var duplicatedHistory = new[] { first.PatchHistory[0], first.PatchHistory[0] };
        var entity = await fixture.DbContext.ProviderResources.SingleAsync(x => x.Id == first.Id);
        entity.PatchHistoryJson = JsonSerializer.Serialize(duplicatedHistory);
        await fixture.DbContext.SaveChangesAsync();

        var compacted = await fixture.Repository.UpsertResourceAsync(upsert with { Title = "Stable title" });

        Assert.Single(compacted.PatchHistory);
        Assert.Single(JsonSerializer.Deserialize<List<ProviderResourcePatchDto>>(entity.PatchHistoryJson!)!);
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

    private sealed class FakeLiveChatYouTubeApiClient : IYouTubeApiClient
    {
        public Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, string? accessToken = null, CancellationToken cancellationToken = default)
            => Task.FromResult<YouTubeViewerStats?>(null);

        public Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, string? accessToken = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeChatPollResult(
                [
                    new YouTubeChatMessage(
                        "super-chat-1",
                        "superChatEvent",
                        "Supporter",
                        "supporter-channel",
                        "https://example.test/avatar.png",
                        "Great stream",
                        new DateTimeOffset(2026, 6, 3, 12, 1, 0, TimeSpan.Zero),
                        "{}",
                        Amount: 12.34m,
                        Currency: "PLN",
                        AmountDisplayString: "12.34 PLN",
                        UserComment: "Great stream",
                        Tier: 2)
                ],
                "next-token",
                TimeSpan.FromSeconds(10)));

        public Task<IReadOnlyList<YouTubeContentItem>> GetVideosAsync(IReadOnlyList<string> videoIds, string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<YouTubeContentItem>>(
            [
                new YouTubeContentItem(
                    "video-1",
                    ProviderResourceKind.Video,
                    "Live",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "{}",
                    LiveChatId: "live-chat-1")
            ]);
    }

    private sealed class FakeYouTubeCredentialResolver : IYouTubeCredentialResolver
    {
        public Task<ProviderAccessTokenDto?> ResolveForChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProviderAccessTokenDto?>(new ProviderAccessTokenDto(Guid.NewGuid(), "access-token"));
    }
}

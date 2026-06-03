using Microsoft.Extensions.Logging.Abstractions;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public sealed class ServiceUnitTests
{
    [Fact]
    public async Task EventIngestionService_DoesNotStoreDuplicateExternalEvent()
    {
        var eventStore = new FakeEventStore { ExistingExternalEventId = "duplicate" };
        var service = new EventIngestionService(eventStore, new TestClock());

        var result = await service.IngestAsync(new ProviderEvent(
            Guid.NewGuid(),
            null,
            null,
            ProviderKind.YouTube,
            StreamEventType.ChatMessage,
            "duplicate",
            "Viewer",
            "viewer-1",
            "Chat",
            "Hello",
            null,
            null,
            new TestClock().UtcNow,
            "{}"));

        Assert.True(result.Duplicate);
        Assert.Empty(eventStore.StoredEvents);
    }

    [Fact]
    public async Task EventIngestionService_StoresNormalizedEventWithClockStoredAt()
    {
        var eventStore = new FakeEventStore();
        var clock = new TestClock();
        var channelId = Guid.NewGuid();
        var service = new EventIngestionService(eventStore, clock);

        var result = await service.IngestAsync(new ProviderEvent(
            channelId,
            null,
            null,
            ProviderKind.Custom,
            StreamEventType.System,
            "event-1",
            null,
            null,
            "Title",
            "Message",
            null,
            null,
            clock.UtcNow.AddMinutes(-5),
            "{}"));

        Assert.True(result.Stored);
        var stored = Assert.Single(eventStore.StoredEvents);
        Assert.Equal(channelId, stored.MonitoredChannelId);
        Assert.Equal(clock.UtcNow, stored.StoredAt);
    }

    [Fact]
    public async Task AudienceIngestionService_DoesNotCreateSecondPeriodWhenRelationshipIsActive()
    {
        var channelId = Guid.NewGuid();
        var audienceMember = new AudienceMember
        {
            Id = Guid.NewGuid(),
            Provider = ProviderKind.YouTube,
            ExternalAudienceId = "audience-1"
        };
        var existingPeriod = new AudienceRelationshipPeriod
        {
            Id = Guid.NewGuid(),
            MonitoredChannelId = channelId,
            AudienceMemberId = audienceMember.Id,
            RelationshipKind = AudienceRelationshipKind.Free,
            StartedAt = new TestClock().UtcNow
        };
        var audienceStore = new FakeAudienceStore(audienceMember, existingPeriod);
        var service = new AudienceIngestionService(audienceStore, new FakeEventIngestionService(), new TestClock());

        var result = await service.IngestRelationshipAsync(new ProviderAudienceRelationship(
            channelId,
            ProviderKind.YouTube,
            "audience-1",
            "Audience",
            null,
            AudienceRelationshipKind.Free,
            new TestClock().UtcNow,
            false,
            "{}"));

        Assert.False(result.CreatedRelationshipPeriod);
        Assert.False(result.Renewed);
        Assert.Empty(audienceStore.CreatedPeriods);
    }

    [Fact]
    public async Task AudienceIngestionService_CreatesRenewalWhenPreviousRelationshipEnded()
    {
        var channelId = Guid.NewGuid();
        var audienceMember = new AudienceMember
        {
            Id = Guid.NewGuid(),
            Provider = ProviderKind.YouTube,
            ExternalAudienceId = "audience-1"
        };
        var previousPeriod = new AudienceRelationshipPeriod
        {
            Id = Guid.NewGuid(),
            MonitoredChannelId = channelId,
            AudienceMemberId = audienceMember.Id,
            RelationshipKind = AudienceRelationshipKind.Free,
            StartedAt = new TestClock().UtcNow.AddDays(-10),
            EndedAt = new TestClock().UtcNow.AddDays(-1)
        };
        var eventIngestion = new FakeEventIngestionService();
        var audienceStore = new FakeAudienceStore(audienceMember, previousPeriod);
        var service = new AudienceIngestionService(audienceStore, eventIngestion, new TestClock());

        var result = await service.IngestRelationshipAsync(new ProviderAudienceRelationship(
            channelId,
            ProviderKind.YouTube,
            "audience-1",
            "Audience",
            null,
            AudienceRelationshipKind.Free,
            new TestClock().UtcNow,
            false,
            "{}"));

        Assert.True(result.Renewed);
        Assert.Single(audienceStore.CreatedPeriods);
        Assert.Single(eventIngestion.Events);
        Assert.Equal(StreamEventType.AudienceRelationshipRenewed, eventIngestion.Events[0].EventType);
    }

    [Fact]
    public async Task AccountDataPoller_ContinuesAfterMonitorFailure()
    {
        var first = new FakeTipProviderMonitor("first", shouldThrow: true);
        var second = new FakeTipProviderMonitor("second", shouldThrow: false);
        var poller = new AccountDataPoller([first, second], NullLogger<AccountDataPoller>.Instance);

        await poller.PollAsync();

        Assert.True(first.WasPolled);
        Assert.True(second.WasPolled);
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeEventStore : IEventStore
    {
        public string? ExistingExternalEventId { get; init; }

        public List<StreamEvent> StoredEvents { get; } = [];

        public Task<bool> EventExistsAsync(Guid monitoredChannelId, ProviderKind provider, string externalEventId, CancellationToken cancellationToken = default)
            => Task.FromResult(externalEventId == ExistingExternalEventId);

        public Task AddEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default)
        {
            StoredEvents.Add(streamEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(Guid? monitoredChannelId, ProviderKind? provider, StreamEventType? eventType, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecentEventDto>>([]);
    }

    private sealed class FakeEventIngestionService : IEventIngestionService
    {
        public List<ProviderEvent> Events { get; } = [];

        public Task<IngestedEventResult> IngestAsync(ProviderEvent providerEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(providerEvent);
            return Task.FromResult(new IngestedEventResult(Guid.NewGuid(), Stored: true, Duplicate: false));
        }
    }

    private sealed class FakeAudienceStore(AudienceMember audienceMember, AudienceRelationshipPeriod? latestPeriod) : IAudienceStore
    {
        public List<AudienceRelationshipPeriod> CreatedPeriods { get; } = [];

        public Task<(AudienceMember AudienceMember, bool Created)> UpsertAudienceMemberAsync(
            ProviderKind provider,
            string externalAudienceId,
            string? displayName,
            string? profileUrl,
            CancellationToken cancellationToken = default)
            => Task.FromResult((audienceMember, Created: false));

        public Task<AudienceRelationshipPeriod?> GetLatestRelationshipPeriodAsync(
            Guid monitoredChannelId,
            Guid audienceMemberId,
            AudienceRelationshipKind relationshipKind,
            CancellationToken cancellationToken = default)
            => Task.FromResult(latestPeriod);

        public Task AddRelationshipPeriodAsync(AudienceRelationshipPeriod period, CancellationToken cancellationToken = default)
        {
            CreatedPeriods.Add(period);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRecentRelationshipsAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudienceRelationshipPeriodDto>>([]);

        public Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRelationshipHistoryAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudienceRelationshipPeriodDto>>([]);
    }

    private sealed class FakeTipProviderMonitor(string name, bool shouldThrow) : ITipProviderMonitor
    {
        public string Name { get; } = name;

        public bool WasPolled { get; private set; }

        public Task PollAsync(CancellationToken cancellationToken = default)
        {
            WasPolled = true;
            return shouldThrow ? throw new InvalidOperationException("monitor failed") : Task.CompletedTask;
        }
    }
}

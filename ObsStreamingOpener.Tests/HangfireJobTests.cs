using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging.Abstractions;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Hangfire;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public sealed class HangfireJobTests
{
    [Fact]
    public void HangfireJobRegistrar_RegistersStreamAndAccountRecurringJobs()
    {
        var recurringJobs = new FakeRecurringJobManager();

        HangfireJobRegistrar.RegisterRecurringJobs(recurringJobs);

        Assert.Contains(recurringJobs.Jobs, x =>
            x.RecurringJobId == "stream-data-sync" &&
            x.CronExpression == Cron.Minutely() &&
            x.Job.Type == typeof(ProviderSyncJobs) &&
            x.Job.Method.Name == nameof(ProviderSyncJobs.PollStreamDataAsync));
        Assert.Contains(recurringJobs.Jobs, x =>
            x.RecurringJobId == "account-data-sync" &&
            x.CronExpression == Cron.Minutely() &&
            x.Job.Type == typeof(ProviderSyncJobs) &&
            x.Job.Method.Name == nameof(ProviderSyncJobs.PollAccountDataAsync));
    }

    [Fact]
    public async Task ProviderSyncJobs_PollsStreamAndAccountDataThroughDedicatedPollers()
    {
        var streamPoller = new FakeStreamDataPoller();
        var accountPoller = new FakeAccountDataPoller();
        var job = new ProviderSyncJobs(streamPoller, accountPoller);

        await job.PollStreamDataAsync();
        await job.PollAccountDataAsync();

        Assert.Equal(1, streamPoller.PollCount);
        Assert.Equal(1, accountPoller.PollCount);
    }

    [Fact]
    public async Task StreamDataPoller_StoresActiveStreamSnapshotsWhenMetricsChange()
    {
        var channelId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();
        var streamSessionId = Guid.NewGuid();
        var clock = new TestClock();
        var channelStore = new FakeChannelStore(new ProviderConnectionDto(
            connectionId,
            channelId,
            ProviderKind.YouTube,
            "live-chat-id",
            "video-id",
            "YouTube"));
        var sessionStore = new FakeStreamSessionStore(new StreamSessionDto(
            streamSessionId,
            channelId,
            "Current stream",
            true,
            clock.UtcNow.AddMinutes(-30),
            null));
        var statsStore = new FakeStatsStore();
        var youtubeClient = new FakeYouTubeApiClient(new YouTubeViewerStats(
            "video-id",
            ConcurrentViewers: 42,
            Likes: 7,
            RawPayloadJson: "{\"source\":\"youtube\"}"));
        var poller = new StreamDataPoller(
            channelStore,
            sessionStore,
            statsStore,
            youtubeClient,
            clock,
            [],
            NullLogger<StreamDataPoller>.Instance);

        await poller.PollAsync();

        Assert.Equal("video-id", youtubeClient.RequestedVideoIds.Single());
        Assert.Equal(2, statsStore.Snapshots.Count);
        Assert.Contains(statsStore.Snapshots, x =>
            x.Metric == MetricKind.ConcurrentViewers &&
            x.Value == 42 &&
            x.Unit == "viewers" &&
            x.MonitoredChannelId == channelId &&
            x.StreamSessionId == streamSessionId &&
            x.ProviderConnectionId == connectionId &&
            x.SnapshotReason == SnapshotReason.ScheduledPoll &&
            x.CapturedAt == clock.UtcNow);
        Assert.Contains(statsStore.Snapshots, x =>
            x.Metric == MetricKind.Likes &&
            x.Value == 7 &&
            x.Unit == "likes" &&
            x.MonitoredChannelId == channelId &&
            x.StreamSessionId == streamSessionId &&
            x.ProviderConnectionId == connectionId &&
            x.SnapshotReason == SnapshotReason.ScheduledPoll &&
            x.CapturedAt == clock.UtcNow);
    }

    [Fact]
    public async Task StreamDataPoller_SkipsStreamMetricsWhenStreamIsNotActive()
    {
        var channelStore = new FakeChannelStore(new ProviderConnectionDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ProviderKind.YouTube,
            "live-chat-id",
            "video-id",
            "YouTube"));
        var youtubeClient = new FakeYouTubeApiClient(new YouTubeViewerStats("video-id", 1, 1, "{}"));
        var statsStore = new FakeStatsStore();
        var poller = new StreamDataPoller(
            channelStore,
            new FakeStreamSessionStore(null),
            statsStore,
            youtubeClient,
            new TestClock(),
            [],
            NullLogger<StreamDataPoller>.Instance);

        await poller.PollAsync();

        Assert.Empty(youtubeClient.RequestedVideoIds);
        Assert.Empty(statsStore.Snapshots);
    }

    [Fact]
    public async Task AccountDataPoller_PollsAccountScopedProviderMonitors()
    {
        var tipply = new FakeTipProviderMonitor("tipply", shouldThrow: false);
        var patronite = new FakeTipProviderMonitor("patronite", shouldThrow: false);
        var poller = new AccountDataPoller([tipply, patronite], NullLogger<AccountDataPoller>.Instance);

        await poller.PollAsync();

        Assert.True(tipply.WasPolled);
        Assert.True(patronite.WasPolled);
    }

    private sealed class FakeStreamDataPoller : IStreamDataPoller
    {
        public int PollCount { get; private set; }

        public Task PollAsync(CancellationToken cancellationToken = default)
        {
            PollCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountDataPoller : IAccountDataPoller
    {
        public int PollCount { get; private set; }

        public Task PollAsync(CancellationToken cancellationToken = default)
        {
            PollCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecurringJobManager : IRecurringJobManager
    {
        public List<RegisteredJob> Jobs { get; } = [];

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
            => Jobs.Add(new RegisteredJob(recurringJobId, job, cronExpression));

        public void RemoveIfExists(string recurringJobId)
        {
        }

        public void Trigger(string recurringJobId)
        {
        }
    }

    private sealed record RegisteredJob(string RecurringJobId, Job Job, string CronExpression);

    private sealed class FakeChannelStore(params ProviderConnectionDto[] connections) : IChannelStore
    {
        public Task<IReadOnlyList<ProviderConnectionDto>> GetEnabledConnectionsAsync(ProviderKind? provider = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderConnectionDto>>(connections
                .Where(x => provider is null || x.Provider == provider)
                .ToList());

        public Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MonitoredAccountDto>>([]);

        public Task<IReadOnlyList<MonitoredChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MonitoredChannelDto>>([]);

        public Task<MonitoredChannelDto?> GetChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => Task.FromResult<MonitoredChannelDto?>(null);

        public Task<MonitoredChannel> GetDefaultChannelEntityAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MonitoredChannelDto> GetDefaultChannelAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeStreamSessionStore(StreamSessionDto? currentSession) : IStreamSessionStore
    {
        public Task<StreamSession> GetOrCreateCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StreamSessionDto?> GetCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => Task.FromResult(currentSession);
    }

    private sealed class FakeStatsStore : IStatsStore
    {
        public List<MetricSnapshot> Snapshots { get; } = [];

        public Task AddMetricSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<bool> AddMetricSnapshotIfChangedAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Snapshots.Add(snapshot);
            return Task.FromResult(true);
        }

        public Task<MetricSnapshot?> GetLatestMetricAsync(Guid monitoredChannelId, MetricKind metric, CancellationToken cancellationToken = default)
            => Task.FromResult<MetricSnapshot?>(null);

        public Task<IReadOnlyList<MetricSnapshot>> GetMetricsAsync(Guid monitoredChannelId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MetricSnapshot>>([]);

        public Task<StreamSessionDto?> GetCurrentStreamAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => Task.FromResult<StreamSessionDto?>(null);
    }

    private sealed class FakeYouTubeApiClient(YouTubeViewerStats? stats) : IYouTubeApiClient
    {
        public List<string> RequestedVideoIds { get; } = [];

        public Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, CancellationToken cancellationToken = default)
        {
            RequestedVideoIds.Add(videoId);
            return Task.FromResult(stats);
        }

        public Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    }
}

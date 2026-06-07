using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging.Abstractions;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Exceptions;
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
        Assert.Contains(recurringJobs.Jobs, x =>
            x.RecurringJobId == "youtube-account-summary-sync" &&
            x.CronExpression == "*/5 * * * *" &&
            x.Job.Method.Name == nameof(ProviderSyncJobs.SyncYouTubeAccountSummaryAsync));
        Assert.Contains(recurringJobs.Jobs, x =>
            x.RecurringJobId == "youtube-live-broadcast-sync" &&
            x.CronExpression == Cron.Minutely() &&
            x.Job.Method.Name == nameof(ProviderSyncJobs.SyncYouTubeLiveBroadcastsAsync));
        Assert.Contains(recurringJobs.Jobs, x =>
            x.RecurringJobId == "youtube-content-discovery-sync" &&
            x.CronExpression == "*/15 * * * *" &&
            x.Job.Method.Name == nameof(ProviderSyncJobs.SyncYouTubeContentDiscoveryAsync));
        Assert.Contains(recurringJobs.Jobs, x =>
            x.RecurringJobId == "youtube-subscriber-sync" &&
            x.CronExpression == "*/30 * * * *" &&
            x.Job.Method.Name == nameof(ProviderSyncJobs.SyncYouTubeVisibleSubscribersAsync));
        Assert.Contains(recurringJobs.Jobs, x =>
            x.RecurringJobId == "youtube-super-chat-sync" &&
            x.CronExpression == "*/10 * * * *" &&
            x.Job.Method.Name == nameof(ProviderSyncJobs.SyncYouTubeSuperChatEventsAsync));
    }

    [Fact]
    public async Task ProviderSyncJobs_PollsStreamAndAccountDataThroughDedicatedPollers()
    {
        var streamPoller = new FakeStreamDataPoller();
        var accountPoller = new FakeAccountDataPoller();
        var youtubeMonitor = new FakeYouTubeAccountDataMonitor();
        var job = new ProviderSyncJobs(streamPoller, accountPoller, youtubeMonitor);

        await job.PollStreamDataAsync();
        await job.PollAccountDataAsync();
        await job.SyncYouTubeAccountSummaryAsync();
        await job.SyncYouTubeLiveBroadcastsAsync();
        await job.SyncYouTubeContentDiscoveryAsync();
        await job.SyncYouTubeVisibleSubscribersAsync();
        await job.SyncYouTubeVideoDetailsAsync(Guid.NewGuid(), "video-id");
        await job.SyncYouTubeVideoCommentsAsync(Guid.NewGuid(), "video-id");

        Assert.Equal(1, streamPoller.PollCount);
        Assert.Equal(1, accountPoller.PollCount);
        Assert.Equal(1, youtubeMonitor.AccountSummaryCount);
        Assert.Equal(1, youtubeMonitor.LiveBroadcastCount);
        Assert.Equal(1, youtubeMonitor.ContentDiscoveryCount);
        Assert.Equal(1, youtubeMonitor.VisibleSubscriberCount);
        Assert.Equal(1, youtubeMonitor.VideoDetailCount);
        Assert.Equal(1, youtubeMonitor.VideoCommentCount);
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
            new FakeYouTubeCredentialResolver("oauth-access-token"),
            clock,
            [],
            NullLogger<StreamDataPoller>.Instance);

        await poller.PollAsync();

        Assert.Equal("video-id", youtubeClient.RequestedVideoIds.Single());
        Assert.Equal("oauth-access-token", youtubeClient.LastAccessToken);
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
            new FakeYouTubeCredentialResolver(null),
            new TestClock(),
            [],
            NullLogger<StreamDataPoller>.Instance);

        await poller.PollAsync();

        Assert.Empty(youtubeClient.RequestedVideoIds);
        Assert.Empty(statsStore.Snapshots);
    }

    [Fact]
    public async Task StreamDataPoller_ContinuesWhenYouTubeMetricRequestFails()
    {
        var channelId = Guid.NewGuid();
        var channelStore = new FakeChannelStore(new ProviderConnectionDto(
            Guid.NewGuid(),
            channelId,
            ProviderKind.YouTube,
            "live-chat-id",
            "video-id",
            "YouTube"));
        var sessionStore = new FakeStreamSessionStore(new StreamSessionDto(
            Guid.NewGuid(),
            channelId,
            "Current stream",
            true,
            new TestClock().UtcNow.AddMinutes(-5),
            null));
        var statsStore = new FakeStatsStore();
        var poller = new StreamDataPoller(
            channelStore,
            sessionStore,
            statsStore,
            new ThrowingYouTubeApiClient(),
            new FakeYouTubeCredentialResolver("oauth-access-token"),
            new TestClock(),
            [],
            NullLogger<StreamDataPoller>.Instance);

        await poller.PollAsync();

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

    private sealed class FakeYouTubeAccountDataMonitor : IYouTubeAccountDataMonitor
    {
        public string Name => "youtube-account-data";

        public int PollCount { get; private set; }

        public int AccountSummaryCount { get; private set; }

        public int LiveBroadcastCount { get; private set; }

        public int ContentDiscoveryCount { get; private set; }

        public int VisibleSubscriberCount { get; private set; }

        public int SuperChatCount { get; private set; }

        public int VideoDetailCount { get; private set; }

        public int VideoCommentCount { get; private set; }

        public Task PollAsync(CancellationToken cancellationToken = default)
        {
            PollCount++;
            return Task.CompletedTask;
        }

        public Task SyncAccountSummaryAsync(CancellationToken cancellationToken = default)
        {
            AccountSummaryCount++;
            return Task.CompletedTask;
        }

        public Task SyncLiveBroadcastsAsync(CancellationToken cancellationToken = default)
        {
            LiveBroadcastCount++;
            return Task.CompletedTask;
        }

        public Task SyncContentDiscoveryAsync(CancellationToken cancellationToken = default)
        {
            ContentDiscoveryCount++;
            return Task.CompletedTask;
        }

        public Task SyncVisibleSubscribersAsync(CancellationToken cancellationToken = default)
        {
            VisibleSubscriberCount++;
            return Task.CompletedTask;
        }

        public Task SyncSuperChatEventsAsync(CancellationToken cancellationToken = default)
        {
            SuperChatCount++;
            return Task.CompletedTask;
        }

        public Task SyncVideoDetailsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default)
        {
            VideoDetailCount++;
            return Task.CompletedTask;
        }

        public Task SyncVideoCommentsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default)
        {
            VideoCommentCount++;
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

        public Task<IReadOnlyList<MetricSnapshot>> GetMetricsAsync(
            Guid monitoredChannelId,
            DateTimeOffset from,
            DateTimeOffset to,
            Guid? providerResourceId = null,
            Guid? streamSessionId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MetricSnapshot>>([]);

        public Task<StreamSessionDto?> GetCurrentStreamAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => Task.FromResult<StreamSessionDto?>(null);
    }

    private sealed class FakeYouTubeApiClient(YouTubeViewerStats? stats) : IYouTubeApiClient
    {
        public List<string> RequestedVideoIds { get; } = [];

        public string? LastAccessToken { get; private set; }

        public Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, string? accessToken = null, CancellationToken cancellationToken = default)
        {
            RequestedVideoIds.Add(videoId);
            LastAccessToken = accessToken;
            return Task.FromResult(stats);
        }

        public Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, string? accessToken = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<YouTubePage<YouTubeSuperChatEvent>> GetSuperChatEventsAsync(string? pageToken, string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubePage<YouTubeSuperChatEvent>([], null, "{}"));
    }

    private sealed class FakeYouTubeCredentialResolver(string? accessToken) : IYouTubeCredentialResolver
    {
        public Task<ProviderAccessTokenDto?> ResolveForChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => Task.FromResult(accessToken is null ? null : new ProviderAccessTokenDto(Guid.NewGuid(), accessToken));
    }

    private sealed class ThrowingYouTubeApiClient : IYouTubeApiClient
    {
        public Task<YouTubeViewerStats?> GetViewerStatsAsync(string videoId, string? accessToken = null, CancellationToken cancellationToken = default)
            => throw new ExternalHttpRequestException(
                "YouTube",
                HttpMethod.Get,
                "https://www.googleapis.com/youtube/v3/videos?id=video-id",
                System.Net.HttpStatusCode.Forbidden,
                "Forbidden",
                "{\"error\":{\"message\":\"quota exceeded\",\"status\":\"PERMISSION_DENIED\"}}",
                "PERMISSION_DENIED",
                "quota exceeded",
                new Dictionary<string, string>());

        public Task<YouTubeChatPollResult> GetLiveChatMessagesAsync(string liveChatId, string? pageToken, string? accessToken = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<YouTubePage<YouTubeSuperChatEvent>> GetSuperChatEventsAsync(string? pageToken, string accessToken, CancellationToken cancellationToken = default)
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

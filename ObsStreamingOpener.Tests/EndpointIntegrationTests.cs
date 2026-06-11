using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Exceptions;
using ObsStreamingOpener.Database;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public sealed class EndpointIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChannelsEndpoint_ReturnsDefaultChannelFromInMemoryDatabase()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Integration account");
            await seeder.CreateChannelAsync(account, externalChannelId: "integration-channel", displayName: "Integration channel");
        });

        var channels = await client.GetFromJsonAsync<List<MonitoredChannelDto>>("/api/channels", JsonOptions);

        var channel = Assert.Single(channels!);
        Assert.True(channel.IsDefault);
        Assert.Equal("Integration channel", channel.DisplayName);
    }

    [Fact]
    public async Task CurrentStreamEndpoint_ReturnsNullWhenChannelHasNoActiveStream()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;
        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Idle account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "idle-channel", displayName: "Idle channel");
            channelId = channel.Id;
        });

        using var response = await client.GetAsync($"/api/channels/{channelId}/stream/current");
        using var missingChannelResponse = await client.GetAsync($"/api/channels/{Guid.NewGuid()}/stream/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("null", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, missingChannelResponse.StatusCode);
    }

    [Fact]
    public async Task DevSampleEvent_IsVisibleThroughCompatibilityRecentEventsEndpoint()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Events account");
            await seeder.CreateChannelAsync(account, externalChannelId: "events-channel", displayName: "Events channel");
        });

        using var postResponse = await client.PostAsJsonAsync("/api/dev/events/sample", new
        {
            provider = "Custom",
            eventType = "Tip",
            actorName = "Endpoint tester",
            message = "Integration tip",
            amount = 25,
            currency = "PLN"
        });

        postResponse.EnsureSuccessStatusCode();
        var events = await client.GetFromJsonAsync<List<RecentEventDto>>("/api/events/recent?limit=5", JsonOptions);

        var streamEvent = Assert.Single(events!);
        Assert.Equal("Endpoint tester", streamEvent.ActorName);
        Assert.Null(streamEvent.StreamSessionId);
        Assert.Equal(25, streamEvent.Amount);
    }

    [Fact]
    public async Task ChannelScopedStats_ReturnsChannelMetricsAfterSampleTip()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Stats account");
            await seeder.CreateChannelAsync(account, externalChannelId: "stats-channel", displayName: "Stats channel");
        });
        var channel = Assert.Single((await client.GetFromJsonAsync<List<MonitoredChannelDto>>("/api/channels", JsonOptions))!);

        using var postResponse = await client.PostAsJsonAsync("/api/dev/events/sample", new
        {
            channelId = channel.Id,
            provider = "Custom",
            eventType = "Tip",
            actorName = "Endpoint tester",
            message = "Integration tip",
            amount = 40,
            currency = "PLN"
        });

        postResponse.EnsureSuccessStatusCode();
        var stats = await client.GetFromJsonAsync<CurrentStatsDto>($"/api/channels/{channel.Id}/stats/current", JsonOptions);

        Assert.Equal(channel.Id, stats!.MonitoredChannelId);
        Assert.Equal(40, stats.TipTotal);
    }

    [Fact]
    public async Task DevAudienceSample_IsVisibleThroughChannelAudienceEndpoint()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Audience account");
            await seeder.CreateChannelAsync(account, externalChannelId: "audience-channel", displayName: "Audience channel");
        });
        var channel = Assert.Single((await client.GetFromJsonAsync<List<MonitoredChannelDto>>("/api/channels", JsonOptions))!);

        using var postResponse = await client.PostAsJsonAsync("/api/dev/events/audience/sample", new
        {
            channelId = channel.Id,
            provider = "Custom",
            externalAudienceId = "endpoint-audience-1",
            displayName = "Endpoint audience",
            relationshipKind = "Free"
        });

        postResponse.EnsureSuccessStatusCode();
        var recent = await client.GetFromJsonAsync<List<AudienceRelationshipPeriodDto>>($"/api/channels/{channel.Id}/audience/recent", JsonOptions);

        var relationship = Assert.Single(recent!);
        Assert.Equal("Endpoint audience", relationship.AudienceDisplayName);
    }

    [Theory]
    [InlineData("/widgets/stats.html")]
    [InlineData("/widgets/recent-events.html")]
    [InlineData("/widgets/goal.html")]
    [InlineData("/widgets/audience.html")]
    [InlineData("/widgets/comment-explorer/index.html")]
    [InlineData("/widgets/alerts/index.html")]
    public async Task Widgets_ReturnOk(string url)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Widget account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: $"widget-channel-{Guid.NewGuid():N}", displayName: "Widget channel");
            await seeder.CreateMetricSnapshotAsync(channel, ObsStreamingOpener.Domain.MetricKind.AudienceMemberCount, 123);
        });

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChannelScopedEndpoints_ReturnSeededDataFromMockDatabase()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;
        Guid audienceMemberId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Seeded account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "seeded-channel", displayName: "Seeded channel");
            var stream = await seeder.CreateStreamSessionAsync(channel, "Seeded stream");
            await seeder.CreateEventAsync(channel, ObsStreamingOpener.Domain.StreamEventType.ChatMessage, actorName: "Seeded actor", message: "Seeded message", streamSession: stream);
            await seeder.CreateMetricSnapshotAsync(channel, ObsStreamingOpener.Domain.MetricKind.ConcurrentViewers, 77, streamSession: stream);
            var audience = await seeder.CreateAudienceRelationshipAsync(channel, "seeded-audience", "Seeded audience");
            channelId = channel.Id;
            audienceMemberId = audience.AudienceMember.Id;
        });

        var events = await client.GetFromJsonAsync<List<RecentEventDto>>($"/api/channels/{channelId}/events/recent", JsonOptions);
        var stats = await client.GetFromJsonAsync<CurrentStatsDto>($"/api/channels/{channelId}/stats/current", JsonOptions);
        var audienceHistory = await client.GetFromJsonAsync<List<AudienceRelationshipPeriodDto>>($"/api/channels/{channelId}/audience/{audienceMemberId}/history", JsonOptions);

        Assert.Single(events!);
        Assert.Equal(77, stats!.ConcurrentViewers);
        Assert.Single(audienceHistory!);
    }

    [Fact]
    public async Task ChannelContentEndpoints_ReturnStoredProviderResourcesAndComments()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Content account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "content-channel", displayName: "Content channel");
            channelId = channel.Id;
            await seeder.CreateProviderResourceAsync(
                channel,
                ProviderResourceKind.Video,
                "video-1",
                "Latest upload",
                publishedAt: DateTimeOffset.UtcNow.AddHours(-1),
                status: "public");
            await seeder.CreateProviderResourceAsync(
                channel,
                ProviderResourceKind.LiveBroadcast,
                "broadcast-1",
                "Next stream",
                scheduledStartAt: DateTimeOffset.UtcNow.AddHours(2),
                status: "upcoming");
            await seeder.CreateEventAsync(
                channel,
                StreamEventType.CommentCreated,
                ProviderKind.YouTube,
                externalEventId: "comment-1",
                actorName: "Commenter",
                message: "Great video",
                occurredAt: DateTimeOffset.UtcNow);
        });

        var recent = await client.GetFromJsonAsync<List<ProviderResourceDto>>($"/api/channels/{channelId}/content/recent?kind=Video", JsonOptions);
        var upcoming = await client.GetFromJsonAsync<List<ProviderResourceDto>>($"/api/channels/{channelId}/content/upcoming", JsonOptions);
        var comments = await client.GetFromJsonAsync<List<RecentEventDto>>($"/api/channels/{channelId}/comments/recent", JsonOptions);
        var overview = await client.GetFromJsonAsync<ChannelContentOverviewDto>($"/api/channels/{channelId}/youtube/overview", JsonOptions);

        Assert.Single(recent!);
        Assert.Equal("Latest upload", recent![0].Title);
        Assert.Single(upcoming!);
        Assert.Equal("Next stream", upcoming![0].Title);
        Assert.Single(comments!);
        Assert.Equal("Great video", comments![0].Message);
        Assert.Equal("Latest upload", overview!.LatestContent!.Title);
        Assert.Equal("Next stream", overview.NextUpcomingStream!.Title);
    }

    [Fact]
    public async Task ResourceScopedEndpoints_ReturnMetricsAndActivityForSelectedResource()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;
        Guid resourceId = default;
        Guid sessionId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Resource scoped account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "resource-scoped-channel", displayName: "Resource scoped channel");
            var resource = await seeder.CreateProviderResourceAsync(
                channel,
                ProviderResourceKind.LiveBroadcast,
                "resource-stream",
                "Resource stream",
                scheduledStartAt: DateTimeOffset.UtcNow.AddMinutes(-20),
                status: "active",
                thumbnailUrl: "https://img.youtube.test/resource-stream.jpg",
                durationSeconds: 1800);
            var stream = await seeder.CreateStreamSessionAsync(channel, "Resource stream", providerResource: resource);
            await seeder.CreateMetricSnapshotAsync(channel, MetricKind.Likes, 12, ProviderKind.YouTube, streamSession: stream, providerResource: resource);
            await seeder.CreateMetricSnapshotAsync(channel, MetricKind.ConcurrentViewers, 44, ProviderKind.YouTube, streamSession: stream, providerResource: resource);
            await seeder.CreateEventAsync(channel, StreamEventType.ChatMessage, ProviderKind.YouTube, externalEventId: "resource-event", message: "Resource event", streamSession: stream, providerResource: resource);
            await seeder.CreateProviderMessageAsync(channel, MessageSource.LiveChat, "resource-message", "Viewer", "Resource message", stream, resource);
            channelId = channel.Id;
            resourceId = resource.Id;
            sessionId = stream.Id;
        });

        var stats = await client.GetFromJsonAsync<CurrentStatsDto>($"/api/channels/{channelId}/stats/current?providerResourceId={resourceId}", JsonOptions);
        var events = await client.GetFromJsonAsync<List<RecentEventDto>>($"/api/channels/{channelId}/events/recent?providerResourceId={resourceId}", JsonOptions);
        var messages = await client.GetFromJsonAsync<List<ProviderMessageDto>>($"/api/channels/{channelId}/messages/recent?providerResourceId={resourceId}", JsonOptions);
        var summary = await client.GetFromJsonAsync<StatsSummaryDto>($"/api/channels/{channelId}/stats/summary?providerResourceId={resourceId}", JsonOptions);

        Assert.Equal(sessionId, stats!.StreamSessionId);
        Assert.Equal(12, stats.Likes);
        Assert.Equal(44, stats.ConcurrentViewers);
        Assert.Single(events!);
        Assert.Single(messages!);
        Assert.Equal(1, summary!.EventCount);
    }

    [Fact]
    public async Task WidgetData_IncludesStoredContentAndRecentComments()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Widget content account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "widget-content-channel", displayName: "Widget content channel");
            channelId = channel.Id;
            await seeder.CreateProviderResourceAsync(channel, ProviderResourceKind.Video, "widget-video", "Widget video", publishedAt: DateTimeOffset.UtcNow.AddMinutes(-30));
            await seeder.CreateProviderResourceAsync(channel, ProviderResourceKind.LiveBroadcast, "widget-broadcast", "Widget stream", scheduledStartAt: DateTimeOffset.UtcNow.AddHours(1));
            await seeder.CreateEventAsync(channel, StreamEventType.CommentCreated, ProviderKind.YouTube, externalEventId: "widget-comment", message: "Widget comment");
        });

        var widget = await client.GetFromJsonAsync<WidgetDataDto>($"/api/widgets/youtube-overview/data?channelId={channelId}", JsonOptions);

        Assert.Contains(widget!.RecentContent!, x => x.Title == "Widget video");
        Assert.Contains(widget.UpcomingContent!, x => x.Title == "Widget stream");
        Assert.Contains(widget.RecentComments!, x => x.Message == "Widget comment");
    }

    [Fact]
    public async Task ChannelMessagesEndpoint_ReturnsSeededProviderMessages()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Messages account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "messages-channel", displayName: "Messages channel");
            channelId = channel.Id;
            await seeder.CreateProviderMessageAsync(channel, MessageSource.LiveChat, "chat-1", "Viewer One", "Hello chat");
        });

        var messages = await client.GetFromJsonAsync<List<ProviderMessageDto>>($"/api/channels/{channelId}/messages/recent?source=LiveChat", JsonOptions);
        var widget = await client.GetFromJsonAsync<JsonElement>($"/api/widgets/comment-explorer/data?channelId={channelId}", JsonOptions);

        var message = Assert.Single(messages!);
        Assert.Equal("Viewer One", message.AuthorDisplayName);
        Assert.Equal("Hello chat", message.MessageText);
        Assert.Equal(channelId, widget.GetProperty("channelId").GetGuid());
        Assert.Single(widget.GetProperty("messages").EnumerateArray());
    }

    [Fact]
    public async Task AlertEndpoints_ReturnActiveAlertsTraceAndWidgetDataForStreamSession()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;
        Guid sessionId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Alert account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "alert-channel", displayName: "Alert channel");
            var session = await seeder.CreateStreamSessionAsync(channel, "Alert stream");
            channelId = channel.Id;
            sessionId = session.Id;
        });

        using var eventResponse = await client.PostAsJsonAsync("/api/dev/events/sample", new
        {
            channelId,
            streamSessionId = sessionId,
            provider = "Custom",
            eventType = "Tip",
            externalEventId = "alert-tip-1",
            actorName = "Alert tester",
            message = "For the overlay",
            amount = 30,
            currency = "PLN"
        });
        eventResponse.EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StreamingOpenerDbContext>();
            var tip = Assert.Single(dbContext.Tips.Where(x => x.MonitoredChannelId == channelId));
            Assert.Equal(30, tip.Amount);
            Assert.Equal("PLN", tip.Currency);
        }

        var active = await client.GetFromJsonAsync<List<StreamAlertDto>>($"/api/channels/{channelId}/alerts/active?streamSessionId={sessionId}", JsonOptions);
        var trace = await client.GetFromJsonAsync<List<StreamEventAlertTraceDto>>($"/api/channels/{channelId}/events/alert-trace?streamSessionId={sessionId}", JsonOptions);
        var widget = await client.GetFromJsonAsync<AlertWidgetDataDto>($"/api/widgets/alerts/data?channelId={channelId}&streamSessionId={sessionId}", JsonOptions);

        var alert = Assert.Single(active!);
        Assert.False(alert.IsSystemAlert);
        Assert.NotNull(alert.StreamEventId);
        Assert.Equal(alert.Id, Assert.Single(trace!).AlertId);
        Assert.Equal(alert.Id, Assert.Single(widget!.Alerts).Id);
        Assert.Equal("shortest-first", widget.Settings.QueueOrdering);

        using var ack = await client.PostAsync($"/api/channels/{channelId}/alerts/{alert.Id}/ack", null);
        Assert.Equal(HttpStatusCode.NoContent, ack.StatusCode);
        var afterAck = await client.GetFromJsonAsync<List<StreamAlertDto>>($"/api/channels/{channelId}/alerts/active?streamSessionId={sessionId}", JsonOptions);
        Assert.Empty(afterAck!);
    }

    [Fact]
    public async Task ManualAlertEndpoint_UsesActiveStreamSession()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;
        Guid sessionId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Manual alert account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "manual-alert-channel", displayName: "Manual alert channel");
            var session = await seeder.CreateStreamSessionAsync(channel, "Manual alert stream");
            channelId = channel.Id;
            sessionId = session.Id;
        });

        var response = await client.PostAsJsonAsync($"/api/channels/{channelId}/alerts/manual", new
        {
            title = "Manual alert",
            message = "Preview me",
            visualStyle = "fireworks",
            durationSeconds = 7
        });
        response.EnsureSuccessStatusCode();
        var alert = await response.Content.ReadFromJsonAsync<StreamAlertDto>(JsonOptions);

        Assert.True(alert!.IsSystemAlert);
        Assert.Equal(sessionId, alert.StreamSessionId);
        Assert.Null(alert.StreamEventId);
    }

    [Fact]
    public async Task AlertWidgetData_ReturnsNearFutureCandidateWhileActiveEndpointDoesNot()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;
        Guid sessionId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Widget alert account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "widget-alert-channel", displayName: "Widget alert channel");
            var session = await seeder.CreateStreamSessionAsync(channel, "Widget alert stream");
            channelId = channel.Id;
            sessionId = session.Id;
        });

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StreamingOpenerDbContext>();
            var now = DateTimeOffset.UtcNow;
            dbContext.StreamAlerts.Add(new StreamAlert
            {
                MonitoredChannelId = channelId,
                StreamSessionId = sessionId,
                Provider = ProviderKind.Custom,
                AlertType = AlertType.System,
                IsSystemAlert = true,
                Title = "Queued candidate",
                VisualStyle = "system",
                DisplayFromUtc = now.AddMinutes(4),
                DisplayUntilUtc = now.AddMinutes(4).AddSeconds(6),
                CreatedAtUtc = now
            });
            await dbContext.SaveChangesAsync();
        }

        var active = await client.GetFromJsonAsync<List<StreamAlertDto>>($"/api/channels/{channelId}/alerts/active?streamSessionId={sessionId}", JsonOptions);
        var widget = await client.GetFromJsonAsync<AlertWidgetDataDto>($"/api/widgets/alerts/data?channelId={channelId}&streamSessionId={sessionId}", JsonOptions);

        Assert.Empty(active!);
        Assert.Equal("Queued candidate", Assert.Single(widget!.Alerts).Title);
    }

    [Fact]
    public async Task StatsEndpoint_ReturnsLatestChangedMetricAfterChangeOnlyPersistence()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Metric account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "metric-channel", displayName: "Metric channel");
            channelId = channel.Id;
        });

        using (var scope = factory.Services.CreateScope())
        {
            var statsStore = scope.ServiceProvider.GetRequiredService<IStatsStore>();
            var dbContext = scope.ServiceProvider.GetRequiredService<StreamingOpenerDbContext>();

            var first = await statsStore.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
            {
                MonitoredChannelId = channelId,
                Provider = ProviderKind.YouTube,
                Metric = MetricKind.AudienceMemberCount,
                SnapshotReason = SnapshotReason.ScheduledPoll,
                Value = 100,
                Unit = "members",
                CapturedAt = DateTimeOffset.UtcNow
            });
            var unchanged = await statsStore.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
            {
                MonitoredChannelId = channelId,
                Provider = ProviderKind.YouTube,
                Metric = MetricKind.AudienceMemberCount,
                SnapshotReason = SnapshotReason.ScheduledPoll,
                Value = 100,
                Unit = "members",
                CapturedAt = DateTimeOffset.UtcNow.AddSeconds(5)
            });
            var changed = await statsStore.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
            {
                MonitoredChannelId = channelId,
                Provider = ProviderKind.YouTube,
                Metric = MetricKind.AudienceMemberCount,
                SnapshotReason = SnapshotReason.ScheduledPoll,
                Value = 125,
                Unit = "members",
                CapturedAt = DateTimeOffset.UtcNow.AddSeconds(10)
            });

            Assert.True(first);
            Assert.False(unchanged);
            Assert.True(changed);
            Assert.Equal(2, dbContext.MetricSnapshots.Count(x => x.MonitoredChannelId == channelId && x.Metric == MetricKind.AudienceMemberCount));
        }

        var stats = await client.GetFromJsonAsync<CurrentStatsDto>($"/api/channels/{channelId}/stats/current", JsonOptions);

        Assert.Equal(125, stats!.AudienceMemberCount);
    }

    [Fact]
    public async Task ConfigEndpoints_CreateAndUpdateAccountsChannelsAndConnections()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var accountResponse = await client.PostAsJsonAsync("/api/config/accounts", new
        {
            displayName = "Dashboard account",
            isDefault = true
        });
        accountResponse.EnsureSuccessStatusCode();
        var account = await accountResponse.Content.ReadFromJsonAsync<MonitoredAccountDto>(JsonOptions);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<StreamingOpenerDbContext>();
            dbContext.MonitoredChannels.Add(new MonitoredChannel
            {
                MonitoredAccountId = account!.Id,
                Provider = ProviderKind.YouTube,
                ExternalChannelId = "dashboard-channel",
                DisplayName = "Dashboard channel",
                IsDefault = true,
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var createChannelResponse = await client.PostAsJsonAsync("/api/config/channels", new
        {
            monitoredAccountId = account!.Id,
            provider = "YouTube",
            externalChannelId = "dashboard-channel",
            displayName = "Dashboard channel",
            url = "https://youtube.com/@dashboard",
            isDefault = true,
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, createChannelResponse.StatusCode);

        var channel = Assert.Single((await client.GetFromJsonAsync<List<MonitoredChannelDto>>("/api/config/channels", JsonOptions))!, x => x.ExternalChannelId == "dashboard-channel");
        using var channelUpdateResponse = await client.PutAsJsonAsync($"/api/config/channels/{channel.Id}", new
        {
            displayName = "Dashboard channel local",
            url = "https://youtube.com/@dashboard",
            isDefault = true,
            isEnabled = true
        });
        channelUpdateResponse.EnsureSuccessStatusCode();
        channel = (await channelUpdateResponse.Content.ReadFromJsonAsync<MonitoredChannelDto>(JsonOptions))!;
        Assert.Equal("Dashboard channel local", channel.DisplayName);

        var connectionResponse = await client.PostAsJsonAsync("/api/config/provider-connections", new
        {
            monitoredChannelId = channel.Id,
            provider = "YouTube",
            externalChannelId = "live-chat-id",
            externalStreamId = "video-id",
            displayName = "Live connection",
            isEnabled = true
        });
        connectionResponse.EnsureSuccessStatusCode();
        var connection = await connectionResponse.Content.ReadFromJsonAsync<ProviderConnectionConfigDto>(JsonOptions);

        using var updateResponse = await client.PutAsJsonAsync($"/api/config/provider-connections/{connection!.Id}", new
        {
            monitoredChannelId = channel.Id,
            provider = "YouTube",
            externalChannelId = "live-chat-id-updated",
            externalStreamId = "video-id-updated",
            displayName = "Updated connection",
            isEnabled = false
        });

        updateResponse.EnsureSuccessStatusCode();
        var connections = await client.GetFromJsonAsync<List<ProviderConnectionConfigDto>>($"/api/config/provider-connections?channelId={channel.Id}", JsonOptions);

        var savedConnection = Assert.Single(connections!);
        Assert.Equal("Updated connection", savedConnection.DisplayName);
        Assert.False(savedConnection.IsEnabled);
    }

    [Fact]
    public async Task ConfigProviderConnection_DeleteRemovesConnection()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid channelId = default;

        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Connection delete account");
            var channel = await seeder.CreateChannelAsync(account, externalChannelId: "connection-delete-channel", displayName: "Connection delete channel");
            channelId = channel.Id;
        });

        var createResponse = await client.PostAsJsonAsync("/api/config/provider-connections", new
        {
            monitoredChannelId = channelId,
            provider = "YouTube",
            externalChannelId = "delete-chat-id",
            externalStreamId = "delete-video-id",
            displayName = "Delete me",
            isEnabled = true
        });
        var connection = await createResponse.Content.ReadFromJsonAsync<ProviderConnectionConfigDto>(JsonOptions);

        using var deleteResponse = await client.DeleteAsync($"/api/config/provider-connections/{connection!.Id}");
        var connections = await client.GetFromJsonAsync<List<ProviderConnectionConfigDto>>($"/api/config/provider-connections?channelId={channelId}", JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty(connections!);
    }

    [Fact]
    public async Task ConfigWidgets_UpsertsWidgetSettings()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/api/config/widgets", new
        {
            widgetKey = "stats",
            widgetType = "stats",
            theme = "light",
            settingsJson = "{\"compact\":true}"
        });

        response.EnsureSuccessStatusCode();
        var widgets = await client.GetFromJsonAsync<List<WidgetConfigurationDto>>("/api/config/widgets", JsonOptions);
        var stats = Assert.Single(widgets!, x => x.WidgetKey == "stats");
        Assert.Equal("light", stats.Theme);
        Assert.Equal("{\"compact\":true}", stats.SettingsJson);
    }

    [Fact]
    public async Task ConfigAlertWidgetSettings_SavesTypedSettings()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/api/config/widgets/alerts", new AlertWidgetSettingsDto(
            "minimal",
            "oldest-first",
            1500,
            12000,
            "/widgets/assets/ping.mp3",
            "https://cdn.example.test/alert.png",
            "none",
            0.35m,
            false), JsonOptions);

        response.EnsureSuccessStatusCode();
        var settings = await client.GetFromJsonAsync<AlertWidgetSettingsDto>("/api/config/widgets/alerts", JsonOptions);

        Assert.Equal("minimal", settings!.Theme);
        Assert.Equal("oldest-first", settings.QueueOrdering);
        Assert.Equal(1500, settings.MinDurationMs);
        Assert.Equal(12000, settings.MaxDurationMs);
        Assert.Equal("/widgets/assets/ping.mp3", settings.DefaultSoundUrl);
        Assert.Equal("https://cdn.example.test/alert.png", settings.DefaultMediaUrl);
        Assert.Equal("none", settings.AnimationPreset);
        Assert.Equal(0.35m, settings.Volume);
        Assert.False(settings.AutoAck);
    }

    [Fact]
    public async Task ConfigAlertWidgetSettings_RejectsInvalidVolume()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync("/api/config/widgets/alerts", new AlertWidgetSettingsDto(
            "default",
            "shortest-first",
            1000,
            6000,
            null,
            null,
            "sparkles",
            2m,
            true), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfigValidation_RejectsMissingRequiredFields()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/config/accounts", new
        {
            displayName = "",
            isDefault = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfigPolling_ReturnsEffectivePollingSettings()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var polling = await client.GetFromJsonAsync<PollingConfigurationDto>("/api/config/polling", JsonOptions);

        Assert.False(polling!.EnableStreamDataPolling);
        Assert.True(polling.StreamDataPollingSeconds >= 5);
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/dashboard/channels")]
    public async Task DashboardFallback_ReturnsIndexHtml(string url)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("<app-root", html);
    }

    [Fact]
    public async Task YouTubeOAuthCallback_WithoutAccountIdCreatesConnectedAccountAndChannel()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var state = await StartYouTubeOAuthAsync(client);
        using var callbackResponse = await client.GetAsync($"/api/auth/youtube/callback?code=first-code&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        Assert.Equal("/dashboard/accounts?connected=success", callbackResponse.Headers.Location?.ToString());

        var connected = await client.GetFromJsonAsync<List<ConnectedAccountDto>>("/api/config/accounts/connected", JsonOptions);
        var account = Assert.Single(connected!, x => x.ExternalAccountId == "google-user-1");
        Assert.Equal("Creator From OAuth", account.DisplayName);
        Assert.Equal("creator@example.test", account.Email);
        Assert.True(account.IsLoggedIn);
        Assert.True(account.HasRefreshToken);
        Assert.False(account.IsExpired);
        Assert.Equal(1, account.ChannelCount);

        var channels = await client.GetFromJsonAsync<List<MonitoredChannelDto>>("/api/config/channels", JsonOptions);
        var channel = Assert.Single(channels!, x => x.ExternalChannelId == "youtube-channel-1");
        Assert.Equal(account.AccountId, channel.MonitoredAccountId);
        Assert.Equal("OAuth Channel", channel.DisplayName);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StreamingOpenerDbContext>();
        var credential = Assert.Single(dbContext.ProviderCredentials);
        Assert.NotEqual("access-token-for-first-code", credential.EncryptedAccessToken);
        Assert.NotEqual("refresh-token-for-first-code", credential.EncryptedRefreshToken);
    }

    [Fact]
    public async Task YouTubeOAuthCallback_WithAccountIdUpdatesExistingAccountCredential()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        Guid accountId = default;
        await factory.SeedDatabaseAsync(async seeder =>
        {
            var account = await seeder.CreateAccountAsync("Existing local account");
            accountId = account.Id;
        });

        var state = await StartYouTubeOAuthAsync(client, accountId);
        using var callbackResponse = await client.GetAsync($"/api/auth/youtube/callback?code=relogin-code&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        var connected = await client.GetFromJsonAsync<List<ConnectedAccountDto>>("/api/config/accounts/connected", JsonOptions);
        var account = Assert.Single(connected!, x => x.AccountId == accountId);
        Assert.Equal(accountId, account.AccountId);
        Assert.True(account.IsLoggedIn);
        Assert.Equal("google-user-1", account.ExternalAccountId);
    }

    [Fact]
    public async Task YouTubeAuthEndpoints_ReloginRefreshAndDisconnectUpdateTokenState()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var state = await StartYouTubeOAuthAsync(client);
        using var callbackResponse = await client.GetAsync($"/api/auth/youtube/callback?code=refresh-code&state={Uri.EscapeDataString(state)}");
        Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
        var account = Assert.Single((await client.GetFromJsonAsync<List<ConnectedAccountDto>>("/api/config/accounts/connected", JsonOptions))!, x => x.ExternalAccountId == "google-user-1");

        var relogin = await client.PostAsync($"/api/auth/youtube/relogin/{account.AccountId}", null);
        relogin.EnsureSuccessStatusCode();
        var reloginUrl = await relogin.Content.ReadFromJsonAsync<YouTubeAuthorizationUrlDto>(JsonOptions);
        Assert.Contains("accounts.google.com", reloginUrl!.AuthorizationUrl);

        var refresh = await client.PostAsync($"/api/auth/youtube/refresh/{account.AccountId}", null);
        refresh.EnsureSuccessStatusCode();
        var refreshed = await refresh.Content.ReadFromJsonAsync<ConnectedAccountDto>(JsonOptions);
        Assert.True(refreshed!.HasRefreshToken);
        Assert.True(refreshed.AccessTokenExpiresAt > account.AccessTokenExpiresAt);

        using var disconnect = await client.DeleteAsync($"/api/auth/youtube/disconnect/{account.AccountId}");
        Assert.Equal(HttpStatusCode.NoContent, disconnect.StatusCode);

        var connected = Assert.Single((await client.GetFromJsonAsync<List<ConnectedAccountDto>>("/api/config/accounts/connected", JsonOptions))!, x => x.AccountId == account.AccountId);
        Assert.False(connected.IsLoggedIn);
        Assert.False(connected.HasRefreshToken);
        Assert.NotNull(connected.DisconnectedAt);

        var channels = await client.GetFromJsonAsync<List<MonitoredChannelDto>>("/api/config/channels", JsonOptions);
        Assert.Contains(channels!, x => x.ExternalChannelId == "youtube-channel-1");
    }

    [Fact]
    public async Task YouTubeSync_ReturnsReadableBadGatewayForProviderFailure()
    {
        await using var baseFactory = new TestWebApplicationFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IYouTubeOAuthService>();
                services.AddSingleton<IYouTubeOAuthService, ThrowingYouTubeOAuthService>();
            });
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsync($"/api/auth/youtube/sync/{Guid.NewGuid()}", null);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("YouTube Data API v3 has not been used", payload);
        Assert.Contains("PERMISSION_DENIED", payload);
        Assert.Contains("channels?part=id", payload);
        Assert.Contains("responseBody", payload);
    }

    [Fact]
    public async Task HangfireDashboard_IsReachable()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/hangfire");

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }

    private static async Task<string> StartYouTubeOAuthAsync(HttpClient client, Guid? accountId = null)
    {
        var url = accountId.HasValue
            ? $"/api/auth/youtube/start?accountId={accountId.Value}"
            : "/api/auth/youtube/start";
        var start = await client.GetFromJsonAsync<YouTubeAuthorizationUrlDto>(url, JsonOptions);
        Assert.NotNull(start);
        var uri = new Uri(start!.AuthorizationUrl);
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("test-client-id", query["client_id"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Contains("https://www.googleapis.com/auth/youtube.readonly", query["scope"].ToString());
        return query["state"].ToString();
    }

    private sealed class ThrowingYouTubeOAuthService : IYouTubeOAuthService
    {
        public YouTubeAuthorizationUrlDto Start(Guid? accountId = null)
            => new("https://accounts.google.com/");

        public Task<Guid> CompleteCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<YouTubeAuthorizationUrlDto> ReloginAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeAuthorizationUrlDto("https://accounts.google.com/"));

        public Task<ConnectedAccountDto?> RefreshAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult<ConnectedAccountDto?>(null);

        public Task<ConnectedAccountDto?> SyncAsync(Guid accountId, CancellationToken cancellationToken = default)
            => throw new ExternalHttpRequestException(
                "YouTube",
                HttpMethod.Get,
                "https://www.googleapis.com/youtube/v3/channels?part=id,snippet,statistics&mine=true",
                HttpStatusCode.Forbidden,
                "Forbidden",
                "{\"error\":{\"message\":\"YouTube Data API v3 has not been used\",\"status\":\"PERMISSION_DENIED\"}}",
                "PERMISSION_DENIED",
                "YouTube Data API v3 has not been used",
                new Dictionary<string, string> { ["x-goog-request-id"] = "request-1" });

        public Task<bool> DisconnectAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}

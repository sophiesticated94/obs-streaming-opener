using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
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
}

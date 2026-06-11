using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Database;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public sealed class IntegrationTestDataSeeder(StreamingOpenerDbContext dbContext, IClock clock) : IIntegrationTestDataSeeder
{
    public async Task<MonitoredAccount> CreateAccountAsync(string displayName = "Test account", bool isDefault = true)
    {
        var account = new MonitoredAccount
        {
            DisplayName = displayName,
            IsDefault = isDefault,
            CreatedAt = clock.UtcNow
        };

        dbContext.MonitoredAccounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account;
    }

    public async Task<MonitoredChannel> CreateChannelAsync(
        MonitoredAccount account,
        ProviderKind provider = ProviderKind.YouTube,
        string externalChannelId = "test-channel",
        string displayName = "Test channel",
        bool isDefault = true)
    {
        var channel = new MonitoredChannel
        {
            MonitoredAccountId = account.Id,
            Provider = provider,
            ExternalChannelId = externalChannelId,
            DisplayName = displayName,
            IsDefault = isDefault,
            IsEnabled = true,
            CreatedAt = clock.UtcNow
        };

        dbContext.MonitoredChannels.Add(channel);
        await dbContext.SaveChangesAsync();
        return channel;
    }

    public async Task<ProviderConnection> CreateProviderConnectionAsync(
        MonitoredChannel channel,
        ProviderKind provider = ProviderKind.YouTube,
        string externalChannelId = "test-live-chat",
        string? externalStreamId = "test-video",
        string? displayName = "Test provider")
    {
        var connection = new ProviderConnection
        {
            MonitoredChannelId = channel.Id,
            Provider = provider,
            ExternalChannelId = externalChannelId,
            ExternalStreamId = externalStreamId,
            DisplayName = displayName,
            IsEnabled = true,
            CreatedAt = clock.UtcNow
        };

        dbContext.ProviderConnections.Add(connection);
        await dbContext.SaveChangesAsync();
        return connection;
    }

    public async Task<StreamSession> CreateStreamSessionAsync(
        MonitoredChannel channel,
        string title = "Test stream",
        bool isActive = true,
        DateTimeOffset? startedAt = null,
        ProviderResource? providerResource = null)
    {
        var session = new StreamSession
        {
            MonitoredChannelId = channel.Id,
            Provider = channel.Provider,
            ExternalSessionId = $"test-session-{Guid.NewGuid():N}",
            Title = title,
            IsActive = isActive,
            ProviderResourceId = providerResource?.Id,
            StartedAt = startedAt ?? clock.UtcNow,
            LastSyncedAt = clock.UtcNow
        };

        dbContext.StreamSessions.Add(session);
        await dbContext.SaveChangesAsync();
        return session;
    }

    public async Task<StreamEvent> CreateEventAsync(
        MonitoredChannel channel,
        StreamEventType eventType,
        ProviderKind provider = ProviderKind.Custom,
        string? externalEventId = null,
        string? actorName = null,
        string? message = null,
        decimal? amount = null,
        string? currency = null,
        StreamSession? streamSession = null,
        AudienceMember? audienceMember = null,
        ProviderResource? providerResource = null,
        DateTimeOffset? occurredAt = null)
    {
        var streamEvent = new StreamEvent
        {
            MonitoredChannelId = channel.Id,
            StreamSessionId = streamSession?.Id,
            AudienceMemberId = audienceMember?.Id,
            ProviderResourceId = providerResource?.Id,
            Provider = provider,
            EventType = eventType,
            ExternalEventId = externalEventId,
            IdentityKey = $"{provider}:{eventType}:{externalEventId ?? Guid.NewGuid().ToString("N")}",
            PayloadHash = "seed",
            ActorName = actorName,
            Title = eventType.ToString(),
            Message = message,
            Value = amount,
            Unit = currency,
            OccurredAt = occurredAt ?? clock.UtcNow,
            StoredAt = clock.UtcNow,
            RawPayloadJson = "{}",
            ContextJson = amount.HasValue || !string.IsNullOrWhiteSpace(currency)
                ? $$"""{"providerAmount":{{amount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}},"providerCurrency":"{{currency}}"}"""
                : null
        };

        dbContext.StreamEvents.Add(streamEvent);
        await dbContext.SaveChangesAsync();
        return streamEvent;
    }

    public async Task<MetricSnapshot> CreateMetricSnapshotAsync(
        MonitoredChannel channel,
        MetricKind metric,
        decimal value,
        ProviderKind provider = ProviderKind.Custom,
        SnapshotReason snapshotReason = SnapshotReason.Manual,
        StreamSession? streamSession = null,
        ProviderConnection? providerConnection = null,
        ProviderResource? providerResource = null,
        DateTimeOffset? capturedAt = null)
    {
        var snapshot = new MetricSnapshot
        {
            MonitoredChannelId = channel.Id,
            StreamSessionId = streamSession?.Id,
            ProviderConnectionId = providerConnection?.Id,
            ProviderResourceId = providerResource?.Id,
            Provider = provider,
            Metric = metric,
            SnapshotReason = snapshotReason,
            Value = value,
            CapturedAt = capturedAt ?? clock.UtcNow,
            RawPayloadJson = "{}"
        };

        dbContext.MetricSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync();
        return snapshot;
    }

    public async Task<ProviderResource> CreateProviderResourceAsync(
        MonitoredChannel channel,
        ProviderResourceKind resourceKind,
        string externalResourceId,
        string? title = null,
        ProviderKind provider = ProviderKind.YouTube,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? scheduledStartAt = null,
        string? status = null,
        string? thumbnailUrl = null,
        int? durationSeconds = null)
    {
        var resource = new ProviderResource
        {
            MonitoredChannelId = channel.Id,
            Provider = provider,
            ResourceKind = resourceKind,
            ExternalResourceId = externalResourceId,
            Title = title,
            Url = resourceKind is ProviderResourceKind.Video or ProviderResourceKind.LiveBroadcast
                ? $"https://www.youtube.com/watch?v={externalResourceId}"
                : null,
            ThumbnailUrl = thumbnailUrl,
            Status = status,
            PublishedAt = publishedAt,
            ScheduledStartAt = scheduledStartAt,
            DurationSeconds = durationSeconds,
            LastSyncedAt = clock.UtcNow,
            RawPayloadJson = "{}"
        };

        dbContext.ProviderResources.Add(resource);
        await dbContext.SaveChangesAsync();
        return resource;
    }

    public async Task<ProviderMessage> CreateProviderMessageAsync(
        MonitoredChannel channel,
        MessageSource source = MessageSource.LiveChat,
        string externalMessageId = "test-message",
        string? authorName = "Test viewer",
        string? messageText = "Hello from test",
        StreamSession? streamSession = null,
        ProviderResource? providerResource = null,
        DateTimeOffset? publishedAt = null)
    {
        var message = new ProviderMessage
        {
            MonitoredChannelId = channel.Id,
            StreamSessionId = streamSession?.Id,
            ProviderResourceId = providerResource?.Id,
            Provider = channel.Provider,
            Source = source,
            ExternalMessageId = externalMessageId,
            IdentityKey = $"{channel.Provider}:{source}:native:{externalMessageId}",
            AuthorDisplayName = authorName,
            MessageText = messageText,
            PublishedAt = publishedAt ?? clock.UtcNow,
            LastSeenAt = clock.UtcNow,
            PayloadSummaryJson = "{}"
        };

        dbContext.ProviderMessages.Add(message);
        await dbContext.SaveChangesAsync();
        return message;
    }

    public async Task<(AudienceMember AudienceMember, AudienceRelationshipPeriod Relationship)> CreateAudienceRelationshipAsync(
        MonitoredChannel channel,
        string externalAudienceId,
        string? displayName = null,
        ProviderKind provider = ProviderKind.Custom,
        AudienceRelationshipKind relationshipKind = AudienceRelationshipKind.Free,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? endedAt = null)
    {
        var audienceMember = new AudienceMember
        {
            Provider = provider,
            ExternalAudienceId = externalAudienceId,
            DisplayName = displayName,
            FirstSeenAt = clock.UtcNow,
            LastSeenAt = clock.UtcNow
        };
        dbContext.AudienceMembers.Add(audienceMember);

        var relationship = new AudienceRelationshipPeriod
        {
            MonitoredChannelId = channel.Id,
            AudienceMember = audienceMember,
            RelationshipKind = relationshipKind,
            StartedAt = startedAt ?? clock.UtcNow,
            EndedAt = endedAt,
            RawPayloadJson = "{}"
        };
        dbContext.AudienceRelationshipPeriods.Add(relationship);

        await dbContext.SaveChangesAsync();
        return (audienceMember, relationship);
    }
}

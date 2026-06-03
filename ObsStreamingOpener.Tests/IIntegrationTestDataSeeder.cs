using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public interface IIntegrationTestDataSeeder
{
    Task<MonitoredAccount> CreateAccountAsync(string displayName = "Test account", bool isDefault = true);

    Task<MonitoredChannel> CreateChannelAsync(
        MonitoredAccount account,
        ProviderKind provider = ProviderKind.YouTube,
        string externalChannelId = "test-channel",
        string displayName = "Test channel",
        bool isDefault = true);

    Task<ProviderConnection> CreateProviderConnectionAsync(
        MonitoredChannel channel,
        ProviderKind provider = ProviderKind.YouTube,
        string externalChannelId = "test-live-chat",
        string? externalStreamId = "test-video",
        string? displayName = "Test provider");

    Task<StreamSession> CreateStreamSessionAsync(
        MonitoredChannel channel,
        string title = "Test stream",
        bool isActive = true,
        DateTimeOffset? startedAt = null);

    Task<StreamEvent> CreateEventAsync(
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
        DateTimeOffset? occurredAt = null);

    Task<MetricSnapshot> CreateMetricSnapshotAsync(
        MonitoredChannel channel,
        MetricKind metric,
        decimal value,
        ProviderKind provider = ProviderKind.Custom,
        SnapshotReason snapshotReason = SnapshotReason.Manual,
        StreamSession? streamSession = null,
        ProviderConnection? providerConnection = null,
        DateTimeOffset? capturedAt = null);

    Task<(AudienceMember AudienceMember, AudienceRelationshipPeriod Relationship)> CreateAudienceRelationshipAsync(
        MonitoredChannel channel,
        string externalAudienceId,
        string? displayName = null,
        ProviderKind provider = ProviderKind.Custom,
        AudienceRelationshipKind relationshipKind = AudienceRelationshipKind.Free,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? endedAt = null);
}

using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Exceptions;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;
using System.Text.Json;

namespace ObsStreamingOpener.Application.Services;

public sealed class YouTubeAccountDataMonitor(
    IChannelStore channelStore,
    IProviderCursorStore cursorStore,
    IProviderResourceStore resourceStore,
    IProviderMessageStore providerMessageStore,
    IStatsStore statsStore,
    IStreamSessionStore streamSessionStore,
    IEventIngestionService eventIngestionService,
    IAudienceIngestionService audienceIngestionService,
    IProviderEventIdentityService identityService,
    IYouTubeCredentialResolver credentialResolver,
    IYouTubeApiClient youtubeClient,
    IClock clock,
    ILogger<YouTubeAccountDataMonitor> logger) : IYouTubeAccountDataMonitor
{
    private const int UploadDetailBatchSize = 10;

    public string Name => "youtube-account-data";

    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        await SyncAccountSummaryAsync(cancellationToken);
        await SyncLiveBroadcastsAsync(cancellationToken);
        await SyncContentDiscoveryAsync(cancellationToken);
        await SyncVisibleSubscribersAsync(cancellationToken);
        await SyncSuperChatEventsAsync(cancellationToken);
    }

    public async Task SyncAccountSummaryAsync(CancellationToken cancellationToken = default)
    {
        await ForEachYouTubeConnectionAsync(async (connection, accessToken) =>
        {
            var summaries = await youtubeClient.GetMyChannelSummariesAsync(accessToken, cancellationToken);
            var summary = summaries.FirstOrDefault(x => x.ChannelId == connection.ExternalChannelId);
            if (summary is null)
            {
                return;
            }

            var payload = SummaryJson("youtube.channels", summary.ChannelId, "channel", summary.Status, null, new Dictionary<string, string?>
            {
                ["title"] = summary.DisplayName,
                ["uploadsPlaylistId"] = summary.UploadsPlaylistId
            });

            await resourceStore.UpsertResourceAsync(ToResource(connection.MonitoredChannelId, ProviderResourceKind.Channel, summary.ChannelId, summary.DisplayName, null, summary.Url, summary.Status, null, null, null, null, payload), cancellationToken);
            await StoreMetricIfPresentAsync(connection, null, MetricKind.AudienceMemberCount, summary.AudienceMemberCount, "members", payload, cancellationToken);
            await StoreMetricIfPresentAsync(connection, null, MetricKind.TotalViews, summary.TotalViews, "views", payload, cancellationToken);
            await StoreMetricIfPresentAsync(connection, null, MetricKind.VideoCount, summary.VideoCount, "videos", payload, cancellationToken);
        }, cancellationToken);
    }

    public async Task SyncLiveBroadcastsAsync(CancellationToken cancellationToken = default)
    {
        await ForEachYouTubeConnectionAsync(async (connection, accessToken) =>
        {
            var statuses = new[] { "active", "upcoming", "completed" };
            var upcomingCount = 0;
            var activeSessionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var status in statuses)
            {
                var broadcasts = await youtubeClient.GetLiveBroadcastsAsync(status, accessToken, cancellationToken);
                foreach (var broadcast in broadcasts)
                {
                    var resource = await UpsertContentAsync(connection.MonitoredChannelId, broadcast, cancellationToken);
                    await IngestBroadcastEventAsync(connection.MonitoredChannelId, broadcast, resource.Id, cancellationToken);
                    if (broadcast.ScheduledStartAt >= clock.UtcNow && !string.Equals(broadcast.Status, "complete", StringComparison.OrdinalIgnoreCase))
                    {
                        upcomingCount++;
                    }

                    if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                    {
                        activeSessionIds.Add(broadcast.Id);
                        await streamSessionStore.UpsertSessionAsync(new ProviderStreamSession(
                            connection.MonitoredChannelId,
                            ProviderKind.YouTube,
                            broadcast.Id,
                            broadcast.Title ?? "YouTube live stream",
                            broadcast.BoundStreamId ?? broadcast.Id,
                            broadcast.LiveChatId,
                            resource.Id,
                            broadcast.ScheduledStartAt,
                            broadcast.ActualStartAt,
                            broadcast.ActualEndAt,
                            broadcast.Status,
                            SummaryJson("youtube.liveBroadcasts", broadcast.Id, "liveBroadcast", broadcast.Status, null, new Dictionary<string, string?>
                            {
                                ["title"] = broadcast.Title,
                                ["boundStreamId"] = broadcast.BoundStreamId,
                                ["liveChatId"] = broadcast.LiveChatId
                            })), cancellationToken);
                    }
                }
            }

            await streamSessionStore.EndMissingActiveSessionsAsync(connection.MonitoredChannelId, ProviderKind.YouTube, activeSessionIds, cancellationToken);
            await StoreMetricIfPresentAsync(connection, null, MetricKind.UpcomingStreamCount, (decimal)upcomingCount, "streams", "{\"source\":\"youtube-liveBroadcasts\"}", cancellationToken);

            var streams = await youtubeClient.GetLiveStreamsAsync(accessToken, cancellationToken);
            foreach (var stream in streams)
            {
                await UpsertContentAsync(connection.MonitoredChannelId, stream, cancellationToken);
            }
        }, cancellationToken);
    }

    public async Task SyncContentDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        await ForEachYouTubeConnectionAsync(async (connection, accessToken) =>
        {
            var summaries = await youtubeClient.GetMyChannelSummariesAsync(accessToken, cancellationToken);
            var uploadsPlaylistId = summaries.FirstOrDefault(x => x.ChannelId == connection.ExternalChannelId)?.UploadsPlaylistId;
            if (!string.IsNullOrWhiteSpace(uploadsPlaylistId))
            {
                var pageToken = await cursorStore.GetCursorAsync(connection.Id, "youtube-uploads-page-token", cancellationToken);
                var uploads = await youtubeClient.GetUploadsAsync(uploadsPlaylistId, pageToken, accessToken, cancellationToken);
                var videoIds = new List<string>();
                foreach (var upload in uploads.Items)
                {
                    var resource = await UpsertContentAsync(connection.MonitoredChannelId, upload, cancellationToken);
                    videoIds.Add(upload.Id);
                    await IngestContentPublishedAsync(connection.MonitoredChannelId, upload, resource.Id, cancellationToken);
                }

                await cursorStore.SetCursorAsync(connection.Id, "youtube-uploads-page-token", uploads.NextPageToken, metadataJson: uploads.RawPayloadJson, cancellationToken: cancellationToken);
                foreach (var batch in videoIds.Distinct().Chunk(UploadDetailBatchSize))
                {
                    var details = await youtubeClient.GetVideosAsync(batch, accessToken, cancellationToken);
                    foreach (var detail in details)
                    {
                        await UpsertVideoDetailsAsync(connection, detail, cancellationToken);
                    }
                }
            }

            var activityToken = await cursorStore.GetCursorAsync(connection.Id, "youtube-activities-page-token", cancellationToken);
            var activities = await youtubeClient.GetChannelActivitiesAsync(connection.ExternalChannelId, activityToken, accessToken, cancellationToken);
            foreach (var activity in activities.Items)
            {
                await eventIngestionService.IngestAsync(new ProviderEvent(
                    connection.MonitoredChannelId,
                    null,
                    null,
                    null,
                    ProviderKind.YouTube,
                    StreamEventType.ContentPublished,
                    $"youtube-activity:{activity.Id}",
                    null,
                    null,
                    activity.Title ?? "YouTube activity",
                    activity.Description ?? activity.ActivityType,
                    null,
                    null,
                    activity.PublishedAt ?? clock.UtcNow,
                    SummaryJson("youtube.activities", activity.Id, "activity", activity.ActivityType, null, new Dictionary<string, string?>
                    {
                        ["title"] = activity.Title,
                        ["resourceId"] = activity.ResourceId
                    })), cancellationToken);
            }

            await cursorStore.SetCursorAsync(connection.Id, "youtube-activities-page-token", activities.NextPageToken, metadataJson: activities.RawPayloadJson, cancellationToken: cancellationToken);
        }, cancellationToken);
    }

    public async Task SyncVisibleSubscribersAsync(CancellationToken cancellationToken = default)
    {
        await ForEachYouTubeConnectionAsync(async (connection, accessToken) =>
        {
            var pageToken = await cursorStore.GetCursorAsync(connection.Id, "youtube-visible-subscribers-page-token", cancellationToken);
            var subscribers = await youtubeClient.GetVisibleSubscribersAsync(pageToken, accessToken, cancellationToken);
            foreach (var subscriber in subscribers.Items)
            {
                await audienceIngestionService.IngestRelationshipAsync(new ProviderAudienceRelationship(
                    connection.MonitoredChannelId,
                    ProviderKind.YouTube,
                    subscriber.ChannelId,
                    subscriber.DisplayName,
                    subscriber.ProfileUrl,
                    AudienceRelationshipKind.Free,
                    subscriber.PublishedAt ?? clock.UtcNow,
                    IsEstimated: true,
                    SummaryJson("youtube.subscriptions", subscriber.ChannelId, "visibleSubscriber", null, null, new Dictionary<string, string?>
                    {
                        ["displayName"] = subscriber.DisplayName,
                        ["profileUrl"] = subscriber.ProfileUrl
                    })), cancellationToken);
            }

            await cursorStore.SetCursorAsync(connection.Id, "youtube-visible-subscribers-page-token", subscribers.NextPageToken, metadataJson: subscribers.RawPayloadJson, cancellationToken: cancellationToken);
        }, cancellationToken);
    }

    public async Task SyncSuperChatEventsAsync(CancellationToken cancellationToken = default)
    {
        await ForEachYouTubeConnectionAsync(async (connection, accessToken) =>
        {
            var cursorName = "youtube-super-chat-events-page-token";
            var pageToken = await cursorStore.GetCursorAsync(connection.Id, cursorName, cancellationToken);
            var superChats = await youtubeClient.GetSuperChatEventsAsync(pageToken, accessToken, cancellationToken);
            foreach (var superChat in superChats.Items.Where(x => string.Equals(x.ChannelId, connection.ExternalChannelId, StringComparison.Ordinal)))
            {
                var payload = SummaryJson("youtube.superChatEvents", superChat.Id, "superChatEvent", null, cursorName, new Dictionary<string, string?>
                {
                    ["supporterChannelId"] = superChat.SupporterChannelId,
                    ["supporterDisplayName"] = superChat.SupporterDisplayName,
                    ["amount"] = superChat.Amount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["currency"] = superChat.Currency,
                    ["amountDisplayString"] = superChat.AmountDisplayString,
                    ["tier"] = superChat.Tier?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });

                await eventIngestionService.IngestAsync(new ProviderEvent(
                    connection.MonitoredChannelId,
                    null,
                    null,
                    null,
                    ProviderKind.YouTube,
                    StreamEventType.Tip,
                    $"youtube-super-chat-event:{superChat.Id}",
                    superChat.SupporterDisplayName,
                    superChat.SupporterChannelId,
                    "YouTube Super Chat",
                    superChat.CommentText ?? superChat.AmountDisplayString,
                    superChat.Amount,
                    superChat.Currency,
                    superChat.CreatedAt ?? clock.UtcNow,
                    payload,
                    Value: superChat.Amount,
                    Unit: superChat.Currency,
                    ContextJson: payload), cancellationToken);
            }

            await cursorStore.SetCursorAsync(connection.Id, cursorName, superChats.NextPageToken, metadataJson: superChats.RawPayloadJson, cancellationToken: cancellationToken);
        }, cancellationToken);
    }

    public async Task SyncVideoDetailsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default)
    {
        var credential = await credentialResolver.ResolveForChannelAsync(monitoredChannelId, cancellationToken);
        if (credential is null)
        {
            return;
        }

        var connection = (await channelStore.GetEnabledConnectionsAsync(ProviderKind.YouTube, cancellationToken))
            .FirstOrDefault(x => x.MonitoredChannelId == monitoredChannelId);
        if (connection is null)
        {
            return;
        }

        var details = await youtubeClient.GetVideosAsync([videoId], credential.AccessToken, cancellationToken);
        foreach (var detail in details)
        {
            await UpsertVideoDetailsAsync(connection, detail, cancellationToken);
        }
    }

    public async Task SyncVideoCommentsAsync(Guid monitoredChannelId, string videoId, CancellationToken cancellationToken = default)
    {
        var credential = await credentialResolver.ResolveForChannelAsync(monitoredChannelId, cancellationToken);
        if (credential is null)
        {
            return;
        }

        var connection = (await channelStore.GetEnabledConnectionsAsync(ProviderKind.YouTube, cancellationToken))
            .FirstOrDefault(x => x.MonitoredChannelId == monitoredChannelId);
        if (connection is null)
        {
            return;
        }

        var pageToken = await cursorStore.GetCursorAsync(connection.Id, $"youtube-comments-{videoId}-page-token", cancellationToken);
        var resource = await resourceStore.GetResourceByExternalIdAsync(monitoredChannelId, ProviderKind.YouTube, videoId, cancellationToken);
        var comments = await youtubeClient.GetCommentThreadsAsync(videoId, pageToken, credential.AccessToken, cancellationToken);
        foreach (var comment in comments.Items)
        {
            var payload = SummaryJson("youtube.commentThreads", comment.Id, "comment", null, null, new Dictionary<string, string?>
            {
                ["author"] = comment.AuthorName,
                ["videoId"] = videoId
            });
            var message = new ProviderMessageUpsert(
                monitoredChannelId,
                null,
                resource?.Id,
                ProviderKind.YouTube,
                MessageSource.VideoComment,
                comment.Id,
                null,
                comment.AuthorChannelId,
                comment.AuthorName,
                null,
                comment.Text,
                comment.PublishedAt ?? clock.UtcNow,
                comment.LikeCount,
                IsOwner: false,
                IsModerator: false,
                IsVerified: false,
                IsSponsor: false,
                null,
                null,
                payload);
            message = message with { IdentityKey = identityService.CreateMessageIdentityKey(message) };
            await providerMessageStore.UpsertMessageAsync(message, cancellationToken);

            await eventIngestionService.IngestAsync(new ProviderEvent(
                monitoredChannelId,
                null,
                null,
                resource?.Id,
                ProviderKind.YouTube,
                StreamEventType.CommentCreated,
                $"youtube-comment:{comment.Id}",
                comment.AuthorName,
                comment.AuthorChannelId,
                "YouTube comment",
                comment.Text,
                null,
                null,
                comment.PublishedAt ?? clock.UtcNow,
                payload), cancellationToken);
        }

        await StoreMetricIfPresentAsync(connection, resource?.Id, MetricKind.CommentCount, (decimal)comments.Items.Count, "comments", comments.RawPayloadJson, cancellationToken);
        await cursorStore.SetCursorAsync(connection.Id, $"youtube-comments-{videoId}-page-token", comments.NextPageToken, metadataJson: comments.RawPayloadJson, cancellationToken: cancellationToken);
    }

    private async Task ForEachYouTubeConnectionAsync(Func<ProviderConnectionDto, string, Task> action, CancellationToken cancellationToken)
    {
        var connections = await channelStore.GetEnabledConnectionsAsync(ProviderKind.YouTube, cancellationToken);
        foreach (var connection in connections)
        {
            try
            {
                var credential = await credentialResolver.ResolveForChannelAsync(connection.MonitoredChannelId, cancellationToken);
                if (credential is null)
                {
                    continue;
                }

                await action(connection, credential.AccessToken);
            }
            catch (ExternalHttpRequestException ex)
            {
                logger.LogWarning(
                    ex,
                    "YouTube account sync failed for connection {ProviderConnectionId}: {StatusCode} {ProviderErrorCode} {ProviderErrorMessage}. Response: {ResponseBody}",
                    connection.Id,
                    (int)ex.StatusCode,
                    ex.ProviderErrorCode,
                    ex.ProviderErrorMessage,
                    ex.ResponseBody);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "YouTube account sync failed for connection {ProviderConnectionId}.", connection.Id);
            }
        }
    }

    private async Task UpsertVideoDetailsAsync(ProviderConnectionDto connection, YouTubeContentItem detail, CancellationToken cancellationToken)
    {
        var resource = await UpsertContentAsync(connection.MonitoredChannelId, detail, cancellationToken);
        await StoreMetricIfPresentAsync(connection, resource.Id, MetricKind.TotalViews, detail.ViewCount, "views", detail.RawPayloadJson, cancellationToken);
        await StoreMetricIfPresentAsync(connection, resource.Id, MetricKind.Likes, detail.LikeCount, "likes", detail.RawPayloadJson, cancellationToken);
        await StoreMetricIfPresentAsync(connection, resource.Id, MetricKind.CommentCount, detail.CommentCount, "comments", detail.RawPayloadJson, cancellationToken);
    }

    private Task<ProviderResourceDto> UpsertContentAsync(Guid monitoredChannelId, YouTubeContentItem item, CancellationToken cancellationToken)
        => resourceStore.UpsertResourceAsync(ToResource(
            monitoredChannelId,
            item.ResourceKind,
            item.Id,
            item.Title,
            item.Description,
            item.Url,
            item.Status,
            item.PublishedAt,
            item.ScheduledStartAt,
            item.ActualStartAt,
            item.ActualEndAt,
            SummaryJson("youtube.resource", item.Id, item.ResourceKind.ToString(), item.Status, null, new Dictionary<string, string?>
            {
                ["title"] = item.Title,
                ["url"] = item.Url,
                ["liveChatId"] = item.LiveChatId,
                ["boundStreamId"] = item.BoundStreamId
            })), cancellationToken);

    private async Task IngestContentPublishedAsync(Guid monitoredChannelId, YouTubeContentItem item, Guid providerResourceId, CancellationToken cancellationToken)
    {
        await eventIngestionService.IngestAsync(new ProviderEvent(
            monitoredChannelId,
            null,
            null,
            providerResourceId,
            ProviderKind.YouTube,
            StreamEventType.ContentPublished,
            $"youtube-content:{providerResourceId}:{item.Id}",
            null,
            item.Id,
            item.Title ?? "YouTube content published",
            item.Url,
            null,
            null,
            item.PublishedAt ?? clock.UtcNow,
            SummaryJson("youtube.content", item.Id, item.ResourceKind.ToString(), item.Status, null, new Dictionary<string, string?>
            {
                ["title"] = item.Title,
                ["url"] = item.Url
            })), cancellationToken);
    }

    private async Task IngestBroadcastEventAsync(Guid monitoredChannelId, YouTubeContentItem item, Guid providerResourceId, CancellationToken cancellationToken)
    {
        var eventType = item.ActualEndAt.HasValue
            ? StreamEventType.LiveBroadcastEnded
            : item.ActualStartAt.HasValue
                ? StreamEventType.LiveBroadcastStarted
                : StreamEventType.LiveBroadcastScheduled;
        var occurredAt = item.ActualEndAt ?? item.ActualStartAt ?? item.ScheduledStartAt ?? item.PublishedAt ?? clock.UtcNow;

        await eventIngestionService.IngestAsync(new ProviderEvent(
            monitoredChannelId,
            null,
            null,
            providerResourceId,
            ProviderKind.YouTube,
            eventType,
            $"youtube-broadcast:{providerResourceId}:{eventType}:{item.Id}:{occurredAt.ToUniversalTime():O}",
            null,
            item.Id,
            item.Title ?? eventType.ToString(),
            item.Url,
            null,
            null,
            occurredAt,
            SummaryJson("youtube.liveBroadcasts", item.Id, "liveBroadcast", item.Status, null, new Dictionary<string, string?>
            {
                ["title"] = item.Title,
                ["url"] = item.Url,
                ["liveChatId"] = item.LiveChatId,
                ["boundStreamId"] = item.BoundStreamId
            })), cancellationToken);
    }

    private async Task StoreMetricIfPresentAsync(ProviderConnectionDto connection, Guid? resourceId, MetricKind metric, long? value, string unit, string rawPayloadJson, CancellationToken cancellationToken)
    {
        if (!value.HasValue)
        {
            return;
        }

        await StoreMetricIfPresentAsync(connection, resourceId, metric, (decimal)value.Value, unit, rawPayloadJson, cancellationToken);
    }

    private async Task StoreMetricIfPresentAsync(ProviderConnectionDto connection, Guid? resourceId, MetricKind metric, decimal value, string unit, string rawPayloadJson, CancellationToken cancellationToken)
    {
        await statsStore.AddMetricSnapshotIfChangedAsync(new MetricSnapshot
        {
            MonitoredChannelId = connection.MonitoredChannelId,
            ProviderConnectionId = connection.Id,
            ProviderResourceId = resourceId,
            Provider = ProviderKind.YouTube,
            Metric = metric,
            SnapshotReason = SnapshotReason.ScheduledPoll,
            Value = value,
            Unit = unit,
            CapturedAt = clock.UtcNow,
            RawPayloadJson = rawPayloadJson
        }, cancellationToken);
    }

    private static ProviderResourceUpsert ToResource(
        Guid monitoredChannelId,
        ProviderResourceKind kind,
        string externalResourceId,
        string? title,
        string? description,
        string? url,
        string? status,
        DateTimeOffset? publishedAt,
        DateTimeOffset? scheduledStartAt,
        DateTimeOffset? actualStartAt,
        DateTimeOffset? actualEndAt,
        string? rawPayloadJson)
        => new(
            monitoredChannelId,
            ProviderKind.YouTube,
            kind,
            externalResourceId,
            title,
            description,
            url,
            status,
            publishedAt,
            scheduledStartAt,
            actualStartAt,
            actualEndAt,
            rawPayloadJson);

    private static string SummaryJson(
        string source,
        string? providerObjectId,
        string objectType,
        string? status,
        string? cursor,
        IReadOnlyDictionary<string, string?> importantFields)
        => JsonSerializer.Serialize(new ProviderPayloadSummary(source, providerObjectId, objectType, null, status, cursor, importantFields));
}

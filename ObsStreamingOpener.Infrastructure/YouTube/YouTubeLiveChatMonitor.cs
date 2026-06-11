using System.Net;
using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Exceptions;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Domain;
using System.Text.Json;

namespace ObsStreamingOpener.Infrastructure.YouTube;

public sealed class YouTubeLiveChatMonitor(
    IChannelStore channelStore,
    IStreamSessionStore streamSessionStore,
    IProviderCursorStore cursorStore,
    IEventIngestionService ingestionService,
    IAudienceIngestionService audienceIngestionService,
    IProviderMessageStore providerMessageStore,
    IProviderEventIdentityService identityService,
    IYouTubeApiClient youtubeApiClient,
    IYouTubeCredentialResolver youtubeCredentialResolver,
    ILogger<YouTubeLiveChatMonitor> logger,
    IActivityPublisher? activityPublisher = null) : IStreamingProviderMonitor
{
    private readonly IActivityPublisher _activityPublisher = activityPublisher ?? new NoOpActivityPublisher();

    public string Name => "youtube-live-chat";

    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        var connections = await channelStore.GetEnabledConnectionsAsync(ProviderKind.YouTube, cancellationToken);
        foreach (var connection in connections.Where(x => !string.IsNullOrWhiteSpace(x.ExternalChannelId)))
        {
            var currentStream = await streamSessionStore.GetCurrentSessionAsync(connection.MonitoredChannelId, cancellationToken);
            if (currentStream is null)
            {
                continue;
            }
            var currentStreamId = currentStream.Id;
            var currentProviderResourceId = currentStream.ProviderResourceId;

            var credential = await youtubeCredentialResolver.ResolveForChannelAsync(connection.MonitoredChannelId, cancellationToken);
            if (credential is null)
            {
                logger.LogDebug("Skipping YouTube live chat for connection {ProviderConnectionId}; OAuth credential is not available.", connection.Id);
                continue;
            }

            var liveChatId = await ResolveLiveChatIdAsync(connection, credential.AccessToken, cancellationToken);
            if (string.IsNullOrWhiteSpace(liveChatId))
            {
                logger.LogDebug("Skipping YouTube live chat for connection {ProviderConnectionId}; active live chat id was not found.", connection.Id);
                continue;
            }

            var cursorName = $"youtube-live-chat:{liveChatId}:page-token";
            var pageToken = await cursorStore.GetCursorAsync(connection.Id, cursorName, cancellationToken);
            YouTubeChatPollResult result;
            try
            {
                result = await youtubeApiClient.GetLiveChatMessagesAsync(liveChatId, pageToken, credential.AccessToken, cancellationToken);
            }
            catch (ExternalHttpRequestException ex) when (IsInvalidPageToken(ex))
            {
                logger.LogInformation("YouTube live chat cursor {CursorName} was stale for connection {ProviderConnectionId}; clearing and retrying without a page token.", cursorName, connection.Id);
                await cursorStore.SetCursorAsync(connection.Id, cursorName, null, cancellationToken: cancellationToken);
                try
                {
                    result = await youtubeApiClient.GetLiveChatMessagesAsync(liveChatId, null, credential.AccessToken, cancellationToken);
                }
                catch (ExternalHttpRequestException retryEx)
                {
                    LogProviderFailure(connection.Id, retryEx);
                    continue;
                }
            }
            catch (ExternalHttpRequestException ex)
            {
                LogProviderFailure(connection.Id, ex);
                continue;
            }

            foreach (var message in result.Messages)
            {
                var payload = SummaryJson("youtube.liveChatMessages", message.Id, "liveChatMessage", null, cursorName, new Dictionary<string, string?>
                {
                    ["author"] = message.AuthorName,
                    ["liveChatId"] = liveChatId,
                    ["type"] = message.Type,
                    ["amount"] = message.Amount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["currency"] = message.Currency,
                    ["amountDisplayString"] = message.AmountDisplayString,
                    ["tier"] = message.Tier?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["memberLevelName"] = message.MemberLevelName,
                    ["giftMembershipsCount"] = message.GiftMembershipsCount?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["memberMonth"] = message.MemberMonth?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["gifterChannelId"] = message.GifterChannelId
                });
                var providerMessage = new ProviderMessageUpsert(
                    connection.MonitoredChannelId,
                    currentStream?.Id,
                    currentStream?.ProviderResourceId,
                    ProviderKind.YouTube,
                    MessageSource.LiveChat,
                    message.Id,
                    null,
                    message.AuthorChannelId,
                    message.AuthorName,
                    message.AuthorProfileImageUrl,
                    message.Message,
                    message.PublishedAt,
                    null,
                    message.IsOwner,
                    message.IsModerator,
                    message.IsVerified,
                    message.IsSponsor,
                    message.Amount,
                    message.Currency,
                    payload);
                providerMessage = providerMessage with { IdentityKey = identityService.CreateMessageIdentityKey(providerMessage) };
                var savedMessage = await providerMessageStore.UpsertMessageAsync(providerMessage, cancellationToken);

                if (message.IsMonetarySupport)
                {
                    var ingestResult = await IngestMonetarySupportAsync(connection.MonitoredChannelId, currentStreamId, currentProviderResourceId, message, payload, cancellationToken);
                    if (ingestResult.Stored && !ingestResult.Duplicate)
                    {
                        await _activityPublisher.PublishMessageCreatedAsync(savedMessage, cancellationToken);
                    }
                    continue;
                }

                if (message.IsMembershipSupport)
                {
                    var ingestResult = await IngestMembershipSupportAsync(connection.MonitoredChannelId, currentStreamId, currentProviderResourceId, message, payload, cancellationToken);
                    if (ingestResult.Stored && !ingestResult.Duplicate)
                    {
                        await _activityPublisher.PublishMessageCreatedAsync(savedMessage, cancellationToken);
                    }
                    continue;
                }

                var eventResult = await ingestionService.IngestAsync(new ProviderEvent(
                    connection.MonitoredChannelId,
                    currentStreamId,
                    null,
                    currentProviderResourceId,
                    ProviderKind.YouTube,
                    StreamEventType.ChatMessage,
                    message.Id,
                    message.AuthorName,
                    message.AuthorChannelId,
                    "YouTube chat",
                    message.Message,
                    null,
                    null,
                    message.PublishedAt,
                    payload), cancellationToken);
                if (eventResult.Stored && !eventResult.Duplicate)
                {
                    await _activityPublisher.PublishMessageCreatedAsync(savedMessage, cancellationToken);
                }
            }

            await cursorStore.SetCursorAsync(
                connection.Id,
                cursorName,
                result.NextPageToken,
                DateTimeOffset.UtcNow.Add(result.PollingInterval),
                cancellationToken: cancellationToken);

            logger.LogDebug("Polled {MessageCount} YouTube chat messages for {ConnectionId}.", result.Messages.Count, connection.Id);
        }
    }

    private async Task<IngestedEventResult> IngestMonetarySupportAsync(Guid monitoredChannelId, Guid streamSessionId, Guid? providerResourceId, YouTubeChatMessage message, string payload, CancellationToken cancellationToken)
    {
        return await ingestionService.IngestAsync(new ProviderEvent(
            monitoredChannelId,
            streamSessionId,
            null,
            providerResourceId,
            ProviderKind.YouTube,
            StreamEventType.Tip,
            message.Id,
            message.AuthorName,
            message.AuthorChannelId,
            message.Type == "superStickerEvent" ? "YouTube Super Sticker" : "YouTube Super Chat",
            message.UserComment ?? message.Message ?? message.AmountDisplayString,
            message.Amount,
            message.Currency,
            message.PublishedAt,
            payload,
            Value: message.Amount,
            Unit: message.Currency,
            ContextJson: payload), cancellationToken);
    }

    private async Task<IngestedEventResult> IngestMembershipSupportAsync(Guid monitoredChannelId, Guid streamSessionId, Guid? providerResourceId, YouTubeChatMessage message, string payload, CancellationToken cancellationToken)
    {
        var eventType = message.Type switch
        {
            "newSponsorEvent" => message.IsMembershipUpgrade == true
                ? StreamEventType.AudienceRelationshipRenewed
                : StreamEventType.AudienceRelationshipStarted,
            "giftMembershipReceivedEvent" => StreamEventType.AudienceRelationshipStarted,
            _ => StreamEventType.AudienceRelationshipRenewed
        };

        var result = await ingestionService.IngestAsync(new ProviderEvent(
            monitoredChannelId,
            streamSessionId,
            null,
            providerResourceId,
            ProviderKind.YouTube,
            eventType,
            message.Id,
            message.AuthorName,
            message.AuthorChannelId,
            MembershipTitle(message),
            MembershipMessage(message),
            message.GiftMembershipsCount ?? message.MemberMonth,
            message.Type is "membershipGiftingEvent" ? "membership-gifts" : "membership",
            message.PublishedAt,
            payload,
            Value: message.GiftMembershipsCount ?? message.MemberMonth,
            Unit: message.Type is "membershipGiftingEvent" ? "membership-gifts" : "membership",
            ContextJson: payload), cancellationToken);

        if (!string.IsNullOrWhiteSpace(message.AuthorChannelId))
        {
            await audienceIngestionService.IngestRelationshipAsync(new ProviderAudienceRelationship(
                monitoredChannelId,
                ProviderKind.YouTube,
                message.AuthorChannelId,
                message.AuthorName,
                message.AuthorProfileImageUrl,
                AudienceRelationshipKind.Paid,
                message.PublishedAt,
                IsEstimated: false,
                payload), cancellationToken);
        }

        return result;
    }

    private static string MembershipTitle(YouTubeChatMessage message)
        => message.Type switch
        {
            "newSponsorEvent" => message.IsMembershipUpgrade == true ? "YouTube membership upgraded" : "YouTube membership started",
            "memberMilestoneChatEvent" => "YouTube member milestone",
            "membershipGiftingEvent" => "YouTube memberships gifted",
            "giftMembershipReceivedEvent" => "YouTube gift membership received",
            _ => "YouTube membership event"
        };

    private static string MembershipMessage(YouTubeChatMessage message)
        => message.Type switch
        {
            "membershipGiftingEvent" => $"{message.AuthorName ?? "Someone"} gifted {message.GiftMembershipsCount ?? 0} memberships",
            "giftMembershipReceivedEvent" => $"{message.AuthorName ?? "Someone"} received a gift membership",
            "memberMilestoneChatEvent" => message.UserComment ?? $"{message.AuthorName ?? "Someone"} has been a member for {message.MemberMonth ?? 0} months",
            _ => message.MemberLevelName ?? message.Message ?? "Membership support"
        };

    private async Task<string?> ResolveLiveChatIdAsync(ProviderConnectionDto connection, string accessToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(connection.ExternalStreamId))
        {
            var videos = await youtubeApiClient.GetVideosAsync([connection.ExternalStreamId], accessToken, cancellationToken);
            var liveChatId = videos.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.LiveChatId))?.LiveChatId;
            if (!string.IsNullOrWhiteSpace(liveChatId))
            {
                return liveChatId;
            }
        }

        var activeBroadcasts = await youtubeApiClient.GetLiveBroadcastsAsync("active", accessToken, cancellationToken);
        return activeBroadcasts
            .Where(x => string.Equals(x.ChannelId, connection.ExternalChannelId, StringComparison.Ordinal))
            .Select(x => x.LiveChatId)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private static bool IsInvalidPageToken(ExternalHttpRequestException ex)
        => ex.StatusCode == HttpStatusCode.BadRequest
            && (ContainsInvalidPageToken(ex.ProviderErrorMessage) || ContainsInvalidPageToken(ex.ResponseBody));

    private static bool ContainsInvalidPageToken(string? value)
        => value?.Contains("page token is not valid", StringComparison.OrdinalIgnoreCase) == true;

    private void LogProviderFailure(Guid providerConnectionId, ExternalHttpRequestException ex)
    {
        logger.LogWarning(
            ex,
            "YouTube live chat poll failed for connection {ProviderConnectionId}: {StatusCode} {ProviderErrorCode} {ProviderErrorMessage}. Response: {ResponseBody}",
            providerConnectionId,
            (int)ex.StatusCode,
            ex.ProviderErrorCode,
            ex.ProviderErrorMessage,
            ex.ResponseBody);
    }

    private static string SummaryJson(
        string source,
        string? providerObjectId,
        string objectType,
        string? status,
        string? cursor,
        IReadOnlyDictionary<string, string?> importantFields)
        => JsonSerializer.Serialize(new ProviderPayloadSummary(source, providerObjectId, objectType, null, status, cursor, importantFields));
}

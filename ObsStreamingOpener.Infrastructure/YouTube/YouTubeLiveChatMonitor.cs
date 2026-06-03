using Microsoft.Extensions.Logging;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Infrastructure.YouTube;

public sealed class YouTubeLiveChatMonitor(
    IStreamSessionStore streamSessionStore,
    IProviderCursorStore cursorStore,
    IEventIngestionService ingestionService,
    IYouTubeApiClient youtubeApiClient,
    ILogger<YouTubeLiveChatMonitor> logger) : IStreamingProviderMonitor
{
    private const string LiveChatPageTokenCursor = "youtube-live-chat-page-token";

    public string Name => "youtube-live-chat";

    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        var connections = await streamSessionStore.GetEnabledConnectionsAsync(ProviderKind.YouTube, cancellationToken);
        foreach (var connection in connections.Where(x => !string.IsNullOrWhiteSpace(x.ExternalChannelId)))
        {
            var pageToken = await cursorStore.GetCursorAsync(connection.Id, LiveChatPageTokenCursor, cancellationToken);
            var result = await youtubeApiClient.GetLiveChatMessagesAsync(connection.ExternalChannelId, pageToken, cancellationToken);

            foreach (var message in result.Messages)
            {
                await ingestionService.IngestAsync(new ProviderEvent(
                    connection.StreamSessionId,
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
                    message.RawPayloadJson), cancellationToken);
            }

            await cursorStore.SetCursorAsync(
                connection.Id,
                LiveChatPageTokenCursor,
                result.NextPageToken,
                DateTimeOffset.UtcNow.Add(result.PollingInterval),
                cancellationToken: cancellationToken);

            logger.LogDebug("Polled {MessageCount} YouTube chat messages for {ConnectionId}.", result.Messages.Count, connection.Id);
        }
    }
}

using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using System.Text.Json;

namespace ObsStreamingOpener.Application.Services;

public sealed class EventIngestionService(
    IEventStore eventStore,
    IClock clock,
    IProviderEventIdentityService? identityService = null,
    IEnumerable<IStreamEventNotificationHandler>? notificationHandlers = null) : IEventIngestionService
{
    private readonly IProviderEventIdentityService _identityService = identityService ?? new ProviderEventIdentityService();
    private readonly IReadOnlyList<IStreamEventNotificationHandler> _notificationHandlers = notificationHandlers?.ToList() ?? [];

    public async Task<IngestedEventResult> IngestAsync(ProviderEvent providerEvent, CancellationToken cancellationToken = default)
    {
        var streamEvent = new StreamEvent
        {
            Id = Guid.NewGuid(),
            MonitoredChannelId = providerEvent.MonitoredChannelId,
            StreamSessionId = providerEvent.StreamSessionId,
            AudienceMemberId = providerEvent.AudienceMemberId,
            ProviderResourceId = providerEvent.ProviderResourceId,
            Provider = providerEvent.Provider,
            EventType = providerEvent.EventType,
            ExternalEventId = providerEvent.ExternalEventId,
            IdentityKey = _identityService.CreateIdentityKey(providerEvent),
            PayloadHash = _identityService.CreatePayloadHash(providerEvent),
            ActorName = providerEvent.ActorName,
            ActorExternalId = providerEvent.ActorExternalId,
            Title = providerEvent.Title,
            Message = providerEvent.Message,
            Value = providerEvent.Value ?? providerEvent.Amount,
            Unit = providerEvent.Unit ?? providerEvent.Currency,
            OccurredAt = providerEvent.OccurredAt,
            StoredAt = clock.UtcNow,
            LastSeenAt = clock.UtcNow,
            RawPayloadJson = providerEvent.RawPayloadJson,
            ContextJson = providerEvent.ContextJson ?? CreateContextJson(providerEvent)
        };

        try
        {
            var result = await eventStore.UpsertEventAsync(streamEvent, cancellationToken);
            if (result.EventId.HasValue)
            {
                streamEvent.Id = result.EventId.Value;
            }

            await NotifyAsync(streamEvent, result, cancellationToken);
            return result;
        }
        catch (NotSupportedException)
        {
            if (!string.IsNullOrWhiteSpace(streamEvent.ExternalEventId)
                && await eventStore.EventExistsAsync(streamEvent.MonitoredChannelId, streamEvent.Provider, streamEvent.ExternalEventId, cancellationToken))
            {
                return new IngestedEventResult(null, Stored: false, Duplicate: true);
            }

            await eventStore.AddEventAsync(streamEvent, cancellationToken);
            var result = new IngestedEventResult(streamEvent.Id, Stored: true, Duplicate: false);
            await NotifyAsync(streamEvent, result, cancellationToken);
            return result;
        }
    }

    private async Task NotifyAsync(StreamEvent streamEvent, IngestedEventResult result, CancellationToken cancellationToken)
    {
        foreach (var handler in _notificationHandlers)
        {
            await handler.HandleAsync(streamEvent, result, cancellationToken);
        }
    }

    private static string? CreateContextJson(ProviderEvent providerEvent)
    {
        if (providerEvent.Amount is null && string.IsNullOrWhiteSpace(providerEvent.Currency))
        {
            return null;
        }

        return JsonSerializer.Serialize(new StreamEventContext(providerEvent.Amount, providerEvent.Currency));
    }

    private sealed record StreamEventContext(decimal? ProviderAmount, string? ProviderCurrency);
}

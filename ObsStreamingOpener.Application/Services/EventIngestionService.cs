using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Application.Services;

public sealed class EventIngestionService(IEventStore eventStore, IClock clock) : IEventIngestionService
{
    public async Task<IngestedEventResult> IngestAsync(ProviderEvent providerEvent, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(providerEvent.ExternalEventId))
        {
            var exists = await eventStore.EventExistsAsync(
                providerEvent.StreamSessionId,
                providerEvent.Provider,
                providerEvent.ExternalEventId,
                cancellationToken);

            if (exists)
            {
                return new IngestedEventResult(null, Stored: false, Duplicate: true);
            }
        }

        var streamEvent = new StreamEvent
        {
            Id = Guid.NewGuid(),
            StreamSessionId = providerEvent.StreamSessionId,
            Provider = providerEvent.Provider,
            EventType = providerEvent.EventType,
            ExternalEventId = providerEvent.ExternalEventId,
            ActorName = providerEvent.ActorName,
            ActorExternalId = providerEvent.ActorExternalId,
            Title = providerEvent.Title,
            Message = providerEvent.Message,
            Amount = providerEvent.Amount,
            Currency = providerEvent.Currency,
            OccurredAt = providerEvent.OccurredAt,
            StoredAt = clock.UtcNow,
            RawPayloadJson = providerEvent.RawPayloadJson
        };

        await eventStore.AddEventAsync(streamEvent, cancellationToken);
        return new IngestedEventResult(streamEvent.Id, Stored: true, Duplicate: false);
    }
}

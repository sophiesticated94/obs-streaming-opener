using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IEventStore
{
    Task<bool> EventExistsAsync(Guid streamSessionId, ProviderKind provider, string externalEventId, CancellationToken cancellationToken = default);

    Task AddEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(ProviderKind? provider, StreamEventType? eventType, int limit, CancellationToken cancellationToken = default);
}

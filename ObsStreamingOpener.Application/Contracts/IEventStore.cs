using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IEventStore
{
    Task<bool> EventExistsAsync(Guid monitoredChannelId, ProviderKind provider, string externalEventId, CancellationToken cancellationToken = default);

    Task AddEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default);

    Task<IngestedEventResult> UpsertEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(
        Guid? monitoredChannelId,
        ProviderKind? provider,
        StreamEventType? eventType,
        int limit,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        Guid? audienceMemberId = null,
        CancellationToken cancellationToken = default);
}

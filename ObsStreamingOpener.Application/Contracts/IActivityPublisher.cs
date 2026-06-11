using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IActivityPublisher
{
    Task PublishEventCreatedAsync(RecentEventDto streamEvent, CancellationToken cancellationToken = default);

    Task PublishMessageCreatedAsync(ProviderMessageDto message, CancellationToken cancellationToken = default);

    Task PublishTipCreatedAsync(TipRealtimeDto tip, CancellationToken cancellationToken = default);
}

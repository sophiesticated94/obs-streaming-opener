using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Services;

public sealed class NoOpStatsPublisher : IStatsPublisher
{
    public Task PublishCurrentStatsAsync(CurrentStatsDto stats, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class NoOpActivityPublisher : IActivityPublisher
{
    public Task PublishEventCreatedAsync(RecentEventDto streamEvent, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishMessageCreatedAsync(ProviderMessageDto message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PublishTipCreatedAsync(TipRealtimeDto tip, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

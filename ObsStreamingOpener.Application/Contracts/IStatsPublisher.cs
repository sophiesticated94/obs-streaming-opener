using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStatsPublisher
{
    Task PublishCurrentStatsAsync(CurrentStatsDto stats, CancellationToken cancellationToken = default);
}

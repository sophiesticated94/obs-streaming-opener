using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IRevenueForecastService
{
    Task<ForecastSummaryDto> GetForecastAsync(Guid? monitoredChannelId, int days, CancellationToken cancellationToken = default);
}

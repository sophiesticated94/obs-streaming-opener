using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStatsQueryService
{
    Task<CurrentStatsDto> GetCurrentStatsAsync(CancellationToken cancellationToken = default);

    Task<StatsSummaryDto> GetSummaryAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);

    Task<WidgetDataDto> GetWidgetDataAsync(string widgetKey, CancellationToken cancellationToken = default);
}

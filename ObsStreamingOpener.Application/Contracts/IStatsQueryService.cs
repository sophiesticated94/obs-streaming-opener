using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStatsQueryService
{
    Task<CurrentStatsDto> GetCurrentStatsAsync(Guid? monitoredChannelId = null, CancellationToken cancellationToken = default);

    Task<CurrentStatsDto> GetCurrentStatsAsync(
        Guid? monitoredChannelId,
        Guid? providerResourceId,
        Guid? streamSessionId,
        CancellationToken cancellationToken = default)
        => GetCurrentStatsAsync(monitoredChannelId, cancellationToken);

    Task<StatsSummaryDto> GetSummaryAsync(
        Guid? monitoredChannelId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        CancellationToken cancellationToken = default);

    Task<WidgetDataDto> GetWidgetDataAsync(string widgetKey, Guid? monitoredChannelId = null, CancellationToken cancellationToken = default);
}

using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Application.Contracts;

public interface IAlertService
{
    Task CreateAlertForEventAsync(StreamEvent streamEvent, IngestedEventResult result, CancellationToken cancellationToken = default);

    Task<StreamAlertDto> CreateManualAlertAsync(Guid monitoredChannelId, ManualAlertRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamAlertDto>> GetActiveAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamAlertDto>> GetRecentAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default);

    Task<bool> AcknowledgeAlertAsync(Guid monitoredChannelId, Guid alertId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamEventAlertTraceDto>> GetEventAlertTraceAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default);

    Task<AlertWidgetDataDto> GetWidgetDataAsync(Guid monitoredChannelId, Guid? streamSessionId, CancellationToken cancellationToken = default);
}

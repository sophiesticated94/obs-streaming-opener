using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStreamAlertStore
{
    Task<StreamAlertDto> AddAlertAsync(StreamAlert alert, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamAlertDto>> GetActiveAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamAlertDto>> GetWidgetCandidateAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamAlertDto>> GetRecentAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default);

    Task<bool> AcknowledgeAlertAsync(Guid monitoredChannelId, Guid alertId, DateTimeOffset acknowledgedAtUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StreamEventAlertTraceDto>> GetEventAlertTraceAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default);
}

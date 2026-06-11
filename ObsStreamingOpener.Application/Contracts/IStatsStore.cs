using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStatsStore
{
    Task AddMetricSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<bool> AddMetricSnapshotIfChangedAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<MetricSnapshot?> GetLatestMetricAsync(Guid monitoredChannelId, MetricKind metric, CancellationToken cancellationToken = default);

    Task<MetricSnapshot?> GetLatestMetricAsync(
        Guid monitoredChannelId,
        MetricKind metric,
        Guid? providerResourceId,
        Guid? streamSessionId,
        CancellationToken cancellationToken = default)
        => GetLatestMetricAsync(monitoredChannelId, metric, cancellationToken);

    Task<IReadOnlyList<MetricSnapshot>> GetMetricsAsync(
        Guid monitoredChannelId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        CancellationToken cancellationToken = default);

    Task<StreamSessionDto?> GetCurrentStreamAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);
}

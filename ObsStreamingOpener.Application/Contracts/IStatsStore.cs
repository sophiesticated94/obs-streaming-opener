using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStatsStore
{
    Task AddMetricSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<MetricSnapshot?> GetLatestMetricAsync(MetricKind metric, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetricSnapshot>> GetMetricsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    Task<StreamSessionDto?> GetCurrentStreamAsync(CancellationToken cancellationToken = default);
}

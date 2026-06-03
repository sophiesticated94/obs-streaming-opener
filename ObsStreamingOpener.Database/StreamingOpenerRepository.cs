using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database;

public sealed class StreamingOpenerRepository(StreamingOpenerDbContext dbContext, IClock clock) :
    IEventStore,
    IStatsStore,
    IProviderCursorStore,
    IStreamSessionStore
{
    public async Task<bool> EventExistsAsync(Guid streamSessionId, ProviderKind provider, string externalEventId, CancellationToken cancellationToken = default)
    {
        return await dbContext.StreamEvents.AnyAsync(
            x => x.StreamSessionId == streamSessionId
                && x.Provider == provider
                && x.ExternalEventId == externalEventId,
            cancellationToken);
    }

    public async Task AddEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default)
    {
        dbContext.StreamEvents.Add(streamEvent);

        if (streamEvent.EventType == StreamEventType.Tip && streamEvent.Amount.HasValue)
        {
            var currentTipTotal = await GetLatestMetricAsync(MetricKind.TipTotal, cancellationToken);
            dbContext.MetricSnapshots.Add(new MetricSnapshot
            {
                StreamSessionId = streamEvent.StreamSessionId,
                Provider = streamEvent.Provider,
                Metric = MetricKind.TipTotal,
                Value = (currentTipTotal?.Value ?? 0) + streamEvent.Amount.Value,
                Unit = streamEvent.Currency,
                CapturedAt = clock.UtcNow,
                RawPayloadJson = streamEvent.RawPayloadJson
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(ProviderKind? provider, StreamEventType? eventType, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 10_000);
        var query = dbContext.StreamEvents.AsNoTracking();

        if (provider.HasValue)
        {
            query = query.Where(x => x.Provider == provider.Value);
        }

        if (eventType.HasValue)
        {
            query = query.Where(x => x.EventType == eventType.Value);
        }

        var events = await query.ToListAsync(cancellationToken);

        return events
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .Select(x => new RecentEventDto(
                x.Id,
                x.Provider,
                x.EventType,
                x.ActorName,
                x.Title,
                x.Message,
                x.Amount,
                x.Currency,
                x.OccurredAt))
            .ToList();
    }

    public async Task AddMetricSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        dbContext.MetricSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MetricSnapshot?> GetLatestMetricAsync(MetricKind metric, CancellationToken cancellationToken = default)
    {
        var metrics = await dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.Metric == metric)
            .ToListAsync(cancellationToken);

        return metrics.OrderByDescending(x => x.CapturedAt).FirstOrDefault();
    }

    public async Task<IReadOnlyList<MetricSnapshot>> GetMetricsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var metrics = await dbContext.MetricSnapshots
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return metrics
            .Where(x => x.CapturedAt >= from && x.CapturedAt <= to)
            .OrderBy(x => x.CapturedAt)
            .ToList();
    }

    public async Task<StreamSessionDto?> GetCurrentStreamAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.StreamSessions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new StreamSessionDto(x.Id, x.Title, x.IsActive, x.StartedAt, x.EndedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StreamSession> GetOrCreateCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        var current = await dbContext.StreamSessions
            .Where(x => x.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is not null)
        {
            return current;
        }

        current = new StreamSession
        {
            Title = "Current stream",
            IsActive = true,
            StartedAt = clock.UtcNow
        };

        dbContext.StreamSessions.Add(current);
        await dbContext.SaveChangesAsync(cancellationToken);
        return current;
    }

    public async Task<IReadOnlyList<ProviderConnectionDto>> GetEnabledConnectionsAsync(ProviderKind? provider = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProviderConnections.AsNoTracking().Where(x => x.IsEnabled);

        if (provider.HasValue)
        {
            query = query.Where(x => x.Provider == provider.Value);
        }

        return await query
            .Select(x => new ProviderConnectionDto(
                x.Id,
                x.StreamSessionId,
                x.Provider,
                x.ExternalChannelId,
                x.ExternalStreamId,
                x.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetCursorAsync(Guid providerConnectionId, string cursorName, CancellationToken cancellationToken = default)
    {
        return await dbContext.ProviderCursors
            .AsNoTracking()
            .Where(x => x.ProviderConnectionId == providerConnectionId && x.CursorName == cursorName)
            .Select(x => x.CursorValue)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetCursorAsync(Guid providerConnectionId, string cursorName, string? cursorValue, DateTimeOffset? expiresAt = null, string? metadataJson = null, CancellationToken cancellationToken = default)
    {
        var cursor = await dbContext.ProviderCursors
            .FirstOrDefaultAsync(x => x.ProviderConnectionId == providerConnectionId && x.CursorName == cursorName, cancellationToken);

        if (cursor is null)
        {
            cursor = new ProviderCursor
            {
                ProviderConnectionId = providerConnectionId,
                CursorName = cursorName
            };
            dbContext.ProviderCursors.Add(cursor);
        }

        cursor.CursorValue = cursorValue;
        cursor.ExpiresAt = expiresAt;
        cursor.MetadataJson = metadataJson;
        cursor.UpdatedAt = clock.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

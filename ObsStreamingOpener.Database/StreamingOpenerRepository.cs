using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database;

public sealed class StreamingOpenerRepository(StreamingOpenerDbContext dbContext, IClock clock) :
    IChannelStore,
    IEventStore,
    IStatsStore,
    IProviderCursorStore,
    IStreamSessionStore,
    IAudienceStore
{
    public async Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.MonitoredAccounts
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName)
            .Select(x => new MonitoredAccountDto(x.Id, x.DisplayName, x.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoredChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.MonitoredChannels
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName)
            .Select(x => new MonitoredChannelDto(
                x.Id,
                x.MonitoredAccountId,
                x.Provider,
                x.ExternalChannelId,
                x.DisplayName,
                x.Url,
                x.IsDefault,
                x.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<MonitoredChannelDto?> GetChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        return await dbContext.MonitoredChannels
            .AsNoTracking()
            .Where(x => x.Id == monitoredChannelId)
            .Select(x => new MonitoredChannelDto(
                x.Id,
                x.MonitoredAccountId,
                x.Provider,
                x.ExternalChannelId,
                x.DisplayName,
                x.Url,
                x.IsDefault,
                x.IsEnabled))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MonitoredChannel> GetDefaultChannelEntityAsync(CancellationToken cancellationToken = default)
    {
        var channels = await dbContext.MonitoredChannels.ToListAsync(cancellationToken);
        var channel = channels
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefault();

        if (channel is not null)
        {
            return channel;
        }

        var account = new MonitoredAccount
        {
            DisplayName = "Default account",
            IsDefault = true,
            CreatedAt = clock.UtcNow
        };
        channel = new MonitoredChannel
        {
            MonitoredAccount = account,
            Provider = ProviderKind.YouTube,
            ExternalChannelId = "default-channel",
            DisplayName = "Default channel",
            IsDefault = true,
            IsEnabled = true,
            CreatedAt = clock.UtcNow
        };

        dbContext.MonitoredChannels.Add(channel);
        await dbContext.SaveChangesAsync(cancellationToken);
        return channel;
    }

    public async Task<MonitoredChannelDto> GetDefaultChannelAsync(CancellationToken cancellationToken = default)
    {
        var channel = await GetDefaultChannelEntityAsync(cancellationToken);
        return new MonitoredChannelDto(
            channel.Id,
            channel.MonitoredAccountId,
            channel.Provider,
            channel.ExternalChannelId,
            channel.DisplayName,
            channel.Url,
            channel.IsDefault,
            channel.IsEnabled);
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
                x.MonitoredChannelId,
                x.Provider,
                x.ExternalChannelId,
                x.ExternalStreamId,
                x.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> EventExistsAsync(Guid monitoredChannelId, ProviderKind provider, string externalEventId, CancellationToken cancellationToken = default)
    {
        return await dbContext.StreamEvents.AnyAsync(
            x => x.MonitoredChannelId == monitoredChannelId
                && x.Provider == provider
                && x.ExternalEventId == externalEventId,
            cancellationToken);
    }

    public async Task AddEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default)
    {
        dbContext.StreamEvents.Add(streamEvent);

        if (streamEvent.EventType == StreamEventType.Tip && streamEvent.Amount.HasValue)
        {
            var currentTipTotal = await GetLatestMetricAsync(streamEvent.MonitoredChannelId, MetricKind.TipTotal, cancellationToken);
            dbContext.MetricSnapshots.Add(new MetricSnapshot
            {
                MonitoredChannelId = streamEvent.MonitoredChannelId,
                StreamSessionId = streamEvent.StreamSessionId,
                Provider = streamEvent.Provider,
                Metric = MetricKind.TipTotal,
                SnapshotReason = SnapshotReason.ProviderEvent,
                Value = (currentTipTotal?.Value ?? 0) + streamEvent.Amount.Value,
                Unit = streamEvent.Currency,
                CapturedAt = clock.UtcNow,
                RawPayloadJson = streamEvent.RawPayloadJson
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(Guid? monitoredChannelId, ProviderKind? provider, StreamEventType? eventType, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 10_000);
        var query = dbContext.StreamEvents.AsNoTracking();

        if (monitoredChannelId.HasValue)
        {
            query = query.Where(x => x.MonitoredChannelId == monitoredChannelId.Value);
        }

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
                x.MonitoredChannelId,
                x.StreamSessionId,
                x.AudienceMemberId,
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

    public async Task<bool> AddMetricSnapshotIfChangedAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var latest = await dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == snapshot.MonitoredChannelId
                && x.Provider == snapshot.Provider
                && x.Metric == snapshot.Metric
                && x.StreamSessionId == snapshot.StreamSessionId
                && x.ProviderConnectionId == snapshot.ProviderConnectionId
                && x.Unit == snapshot.Unit)
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.Value == snapshot.Value)
        {
            return false;
        }

        dbContext.MetricSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MetricSnapshot?> GetLatestMetricAsync(Guid monitoredChannelId, MetricKind metric, CancellationToken cancellationToken = default)
    {
        var metrics = await dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.Metric == metric)
            .ToListAsync(cancellationToken);

        return metrics.OrderByDescending(x => x.CapturedAt).FirstOrDefault();
    }

    public async Task<IReadOnlyList<MetricSnapshot>> GetMetricsAsync(Guid monitoredChannelId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var metrics = await dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId)
            .ToListAsync(cancellationToken);

        return metrics
            .Where(x => x.CapturedAt >= from && x.CapturedAt <= to)
            .OrderBy(x => x.CapturedAt)
            .ToList();
    }

    public async Task<StreamSessionDto?> GetCurrentStreamAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.StreamSessions
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.IsActive)
            .ToListAsync(cancellationToken);

        return sessions
            .OrderByDescending(x => x.StartedAt)
            .Select(x => new StreamSessionDto(x.Id, x.MonitoredChannelId, x.Title, x.IsActive, x.StartedAt, x.EndedAt))
            .FirstOrDefault();
    }

    public async Task<StreamSession> GetOrCreateCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.StreamSessions
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.IsActive)
            .ToListAsync(cancellationToken);
        var current = sessions.OrderByDescending(x => x.StartedAt).FirstOrDefault();

        if (current is not null)
        {
            return current;
        }

        current = new StreamSession
        {
            MonitoredChannelId = monitoredChannelId,
            Title = "Current stream",
            IsActive = true,
            StartedAt = clock.UtcNow
        };

        dbContext.StreamSessions.Add(current);
        await dbContext.SaveChangesAsync(cancellationToken);
        return current;
    }

    public Task<StreamSessionDto?> GetCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
        => GetCurrentStreamAsync(monitoredChannelId, cancellationToken);

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

    public async Task<(AudienceMember AudienceMember, bool Created)> UpsertAudienceMemberAsync(ProviderKind provider, string externalAudienceId, string? displayName, string? profileUrl, CancellationToken cancellationToken = default)
    {
        var audienceMember = await dbContext.AudienceMembers
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ExternalAudienceId == externalAudienceId, cancellationToken);

        if (audienceMember is null)
        {
            audienceMember = new AudienceMember
            {
                Provider = provider,
                ExternalAudienceId = externalAudienceId,
                DisplayName = displayName,
                ProfileUrl = profileUrl,
                FirstSeenAt = clock.UtcNow,
                LastSeenAt = clock.UtcNow
            };
            dbContext.AudienceMembers.Add(audienceMember);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (audienceMember, Created: true);
        }

        audienceMember.DisplayName = displayName ?? audienceMember.DisplayName;
        audienceMember.ProfileUrl = profileUrl ?? audienceMember.ProfileUrl;
        audienceMember.LastSeenAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (audienceMember, Created: false);
    }

    public async Task<AudienceRelationshipPeriod?> GetLatestRelationshipPeriodAsync(Guid monitoredChannelId, Guid audienceMemberId, AudienceRelationshipKind relationshipKind, CancellationToken cancellationToken = default)
    {
        var periods = await dbContext.AudienceRelationshipPeriods
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId
                && x.AudienceMemberId == audienceMemberId
                && x.RelationshipKind == relationshipKind)
            .ToListAsync(cancellationToken);

        return periods.OrderByDescending(x => x.StartedAt).FirstOrDefault();
    }

    public async Task AddRelationshipPeriodAsync(AudienceRelationshipPeriod period, CancellationToken cancellationToken = default)
    {
        dbContext.AudienceRelationshipPeriods.Add(period);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRecentRelationshipsAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var periods = await dbContext.AudienceRelationshipPeriods
            .AsNoTracking()
            .Include(x => x.AudienceMember)
            .Where(x => x.MonitoredChannelId == monitoredChannelId)
            .ToListAsync(cancellationToken);

        return periods
            .OrderByDescending(x => x.StartedAt)
            .Take(limit)
            .Select(ToDto)
            .ToList();
    }

    public async Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRelationshipHistoryAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default)
    {
        var periods = await dbContext.AudienceRelationshipPeriods
            .AsNoTracking()
            .Include(x => x.AudienceMember)
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.AudienceMemberId == audienceMemberId)
            .ToListAsync(cancellationToken);

        return periods
            .OrderByDescending(x => x.StartedAt)
            .Select(ToDto)
            .ToList();
    }

    private static AudienceRelationshipPeriodDto ToDto(AudienceRelationshipPeriod period)
    {
        return new AudienceRelationshipPeriodDto(
            period.Id,
            period.MonitoredChannelId,
            period.AudienceMemberId,
            period.RelationshipKind,
            period.StartedAt,
            period.EndedAt,
            period.IsEstimated,
            period.AudienceMember?.DisplayName);
    }
}

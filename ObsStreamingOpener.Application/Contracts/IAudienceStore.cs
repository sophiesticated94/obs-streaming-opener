using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IAudienceStore
{
    Task<(AudienceMember AudienceMember, bool Created)> UpsertAudienceMemberAsync(
        ProviderKind provider,
        string externalAudienceId,
        string? displayName,
        string? profileUrl,
        CancellationToken cancellationToken = default);

    Task<AudienceRelationshipPeriod?> GetLatestRelationshipPeriodAsync(
        Guid monitoredChannelId,
        Guid audienceMemberId,
        AudienceRelationshipKind relationshipKind,
        CancellationToken cancellationToken = default);

    Task AddRelationshipPeriodAsync(AudienceRelationshipPeriod period, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRecentRelationshipsAsync(Guid monitoredChannelId, int limit, bool includeRevenue = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRelationshipHistoryAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default);

    Task<AudienceRevenueSummaryDto> GetAudienceRevenueAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default);
}

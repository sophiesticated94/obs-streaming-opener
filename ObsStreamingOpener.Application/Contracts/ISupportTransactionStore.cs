using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface ISupportTransactionStore
{
    Task<Tip?> GetTipByProviderExternalIdAsync(ProviderKind provider, string externalTipId, CancellationToken cancellationToken = default);

    Task<Tip?> GetTipByIdAsync(Guid tipId, CancellationToken cancellationToken = default);

    Task<Tip> UpsertTipDetailsAsync(Guid streamEventId, ProviderTipRecord record, Guid? refundedTipId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tip>> QueryTipsAsync(RevenueSummaryQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AudienceRelationshipPeriod>> QueryPaidRelationshipsAsync(Guid? monitoredChannelId, DateTimeOffset until, CancellationToken cancellationToken = default);
}

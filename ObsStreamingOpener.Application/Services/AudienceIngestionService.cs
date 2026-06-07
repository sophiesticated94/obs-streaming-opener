using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class AudienceIngestionService(
    IAudienceStore audienceStore,
    IEventIngestionService eventIngestionService,
    IClock clock) : IAudienceIngestionService
{
    public async Task<AudienceRelationshipResult> IngestRelationshipAsync(ProviderAudienceRelationship relationship, CancellationToken cancellationToken = default)
    {
        var (audienceMember, createdAudienceMember) = await audienceStore.UpsertAudienceMemberAsync(
            relationship.Provider,
            relationship.ExternalAudienceId,
            relationship.DisplayName,
            relationship.ProfileUrl,
            cancellationToken);

        var latestPeriod = await audienceStore.GetLatestRelationshipPeriodAsync(
            relationship.MonitoredChannelId,
            audienceMember.Id,
            relationship.RelationshipKind,
            cancellationToken);

        if (latestPeriod is not null && latestPeriod.EndedAt is null)
        {
            return new AudienceRelationshipResult(
                audienceMember.Id,
                latestPeriod.Id,
                createdAudienceMember,
                CreatedRelationshipPeriod: false,
                Renewed: false);
        }

        var renewed = latestPeriod is not null;
        var eventType = renewed ? StreamEventType.AudienceRelationshipRenewed : StreamEventType.AudienceRelationshipStarted;
        var startedAt = relationship.StartedAt == default ? clock.UtcNow : relationship.StartedAt;
        var externalEventId = $"{relationship.Provider}:audience:{eventType}:{relationship.MonitoredChannelId:N}:{relationship.ExternalAudienceId}:{relationship.RelationshipKind}:{startedAt.ToUniversalTime():O}";
        var eventResult = await eventIngestionService.IngestAsync(new ProviderEvent(
            relationship.MonitoredChannelId,
            null,
            audienceMember.Id,
            null,
            relationship.Provider,
            eventType,
            externalEventId,
            relationship.DisplayName,
            relationship.ExternalAudienceId,
            renewed ? "Audience relationship renewed" : "Audience relationship started",
            relationship.RelationshipKind.ToString(),
            null,
            null,
            startedAt,
            relationship.RawPayloadJson), cancellationToken);

        var period = new AudienceRelationshipPeriod
        {
            Id = Guid.NewGuid(),
            MonitoredChannelId = relationship.MonitoredChannelId,
            AudienceMemberId = audienceMember.Id,
            RelationshipKind = relationship.RelationshipKind,
            StartedAt = startedAt,
            IsEstimated = relationship.IsEstimated,
            SupportExternalId = relationship.SupportExternalId,
            TierName = relationship.TierName,
            Status = relationship.Status,
            BillingCadence = relationship.BillingCadence,
            Amount = relationship.Amount,
            Currency = relationship.Currency,
            LastChargeAt = relationship.LastChargeAt,
            NextChargeAt = relationship.NextChargeAt,
            CancelledAt = relationship.CancelledAt,
            SourceEventId = eventResult.EventId,
            RawPayloadJson = relationship.RawPayloadJson
        };

        await audienceStore.AddRelationshipPeriodAsync(period, cancellationToken);

        return new AudienceRelationshipResult(
            audienceMember.Id,
            period.Id,
            createdAudienceMember,
            CreatedRelationshipPeriod: true,
            renewed);
    }
}

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
        var eventResult = await eventIngestionService.IngestAsync(new ProviderEvent(
            relationship.MonitoredChannelId,
            null,
            audienceMember.Id,
            relationship.Provider,
            renewed ? StreamEventType.AudienceRelationshipRenewed : StreamEventType.AudienceRelationshipStarted,
            $"audience-{relationship.MonitoredChannelId:N}-{relationship.Provider}-{relationship.ExternalAudienceId}-{relationship.StartedAt:O}",
            relationship.DisplayName,
            relationship.ExternalAudienceId,
            renewed ? "Audience relationship renewed" : "Audience relationship started",
            relationship.RelationshipKind.ToString(),
            null,
            null,
            relationship.StartedAt == default ? clock.UtcNow : relationship.StartedAt,
            relationship.RawPayloadJson), cancellationToken);

        var period = new AudienceRelationshipPeriod
        {
            Id = Guid.NewGuid(),
            MonitoredChannelId = relationship.MonitoredChannelId,
            AudienceMemberId = audienceMember.Id,
            RelationshipKind = relationship.RelationshipKind,
            StartedAt = relationship.StartedAt == default ? clock.UtcNow : relationship.StartedAt,
            IsEstimated = relationship.IsEstimated,
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

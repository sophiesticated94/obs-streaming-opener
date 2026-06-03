using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IAudienceIngestionService
{
    Task<AudienceRelationshipResult> IngestRelationshipAsync(ProviderAudienceRelationship relationship, CancellationToken cancellationToken = default);
}

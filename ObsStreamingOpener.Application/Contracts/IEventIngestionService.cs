using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IEventIngestionService
{
    Task<IngestedEventResult> IngestAsync(ProviderEvent providerEvent, CancellationToken cancellationToken = default);
}

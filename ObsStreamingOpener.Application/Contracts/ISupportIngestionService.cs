using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface ISupportIngestionService
{
    Task<TipIngestionResult> IngestTipAsync(ProviderTipRecord record, CancellationToken cancellationToken = default);

    Task IngestPatronAsync(ProviderPatronRecord record, CancellationToken cancellationToken = default);
}

using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IBrowserSessionFactory
{
    Task<BrowserLoginResultDto> StartManualLoginAsync(ProviderKind provider, CancellationToken cancellationToken = default);
}

using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderBrowserScraper
{
    ProviderKind Provider { get; }

    IAsyncEnumerable<ProviderTipRecord> ScrapeTipsAsync(CancellationToken cancellationToken = default);
}

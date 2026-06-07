using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Options;

namespace ObsStreamingOpener.Infrastructure.Providers;

public sealed class EmptySupportProviderAdapter(
    ProviderKind provider,
    IOptionsMonitor<SupportProviderOptions> options) : ISupportProviderAdapter
{
    public ProviderKind Provider { get; } = provider;

    public bool Enabled => options.Get(Provider.ToString()).Enabled;

    public async IAsyncEnumerable<ProviderTipRecord> GetTipsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<ProviderPatronRecord> GetPatronsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

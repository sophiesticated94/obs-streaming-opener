using Microsoft.Extensions.Caching.Memory;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Infrastructure.Providers;

public sealed class CachedStreamProviderDataProvider(
    IStreamProviderDataProvider inner,
    IMemoryCache memoryCache) : IStreamProviderDataProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    public Task<StreamProviderDataSnapshot?> GetCurrentStreamDataAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
        => memoryCache.GetOrCreateAsync(
            $"stream-provider-data:{monitoredChannelId:N}",
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return inner.GetCurrentStreamDataAsync(monitoredChannelId, cancellationToken);
            });
}

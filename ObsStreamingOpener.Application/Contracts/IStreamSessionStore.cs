using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStreamSessionStore
{
    Task<StreamSession> GetOrCreateCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);

    Task<StreamSessionDto> UpsertSessionAsync(ProviderStreamSession session, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    Task EndMissingActiveSessionsAsync(Guid monitoredChannelId, ProviderKind provider, IReadOnlySet<string> activeExternalSessionIds, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    Task<StreamSessionDto?> GetCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default);

    Task<StreamSessionDto?> GetSessionByProviderResourceAsync(Guid monitoredChannelId, Guid providerResourceId, CancellationToken cancellationToken = default)
        => Task.FromResult<StreamSessionDto?>(null);
}

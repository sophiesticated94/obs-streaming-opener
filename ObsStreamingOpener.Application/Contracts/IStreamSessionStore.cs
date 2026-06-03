using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IStreamSessionStore
{
    Task<StreamSession> GetOrCreateCurrentSessionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderConnectionDto>> GetEnabledConnectionsAsync(ProviderKind? provider = null, CancellationToken cancellationToken = default);
}

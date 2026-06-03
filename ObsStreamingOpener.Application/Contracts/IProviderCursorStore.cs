namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderCursorStore
{
    Task<string?> GetCursorAsync(Guid providerConnectionId, string cursorName, CancellationToken cancellationToken = default);

    Task SetCursorAsync(Guid providerConnectionId, string cursorName, string? cursorValue, DateTimeOffset? expiresAt = null, string? metadataJson = null, CancellationToken cancellationToken = default);
}

using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Infrastructure.Browser;

public sealed class LoginStateService(
    IProviderBrowserSessionStore browserSessionStore,
    ICredentialProtector credentialProtector) : ILoginStateService
{
    public async Task<bool> HasStateAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        var session = await browserSessionStore.GetBrowserSessionAsync(provider, cancellationToken);
        return session is not null
            && session.DisconnectedAt is null
            && !string.IsNullOrWhiteSpace(session.EncryptedStorageStateJson);
    }

    public async Task<string> GetStatePathAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        var path = CreateTemporaryStatePath(provider);
        var session = await browserSessionStore.GetBrowserSessionAsync(provider, cancellationToken);
        if (session is null || session.DisconnectedAt is not null || string.IsNullOrWhiteSpace(session.EncryptedStorageStateJson))
        {
            return path;
        }

        var stateJson = credentialProtector.Unprotect(session.EncryptedStorageStateJson);
        await File.WriteAllTextAsync(path, stateJson, cancellationToken);
        return path;
    }

    public async Task SaveStateAsync(ProviderKind provider, string storageStateJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageStateJson))
        {
            await browserSessionStore.MarkBrowserSessionStatusAsync(provider, "NeedsLogin", cancellationToken: cancellationToken);
            return;
        }

        var encrypted = credentialProtector.Protect(storageStateJson);
        await browserSessionStore.UpsertBrowserSessionAsync(provider, encrypted, "Ready", cancellationToken);
    }

    public Task DeleteTemporaryStateAsync(string statePath, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(statePath) && File.Exists(statePath))
        {
            File.Delete(statePath);
        }

        return Task.CompletedTask;
    }

    public async Task<BrowserLoginResultDto> ClearStateAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        await browserSessionStore.ClearBrowserSessionAsync(provider, cancellationToken);
        return new BrowserLoginResultDto(provider, "Cleared", "Encrypted browser storage state was removed.", null);
    }

    private static string CreateTemporaryStatePath(ProviderKind provider)
    {
        var directory = Path.Combine(Path.GetTempPath(), "obs-streaming-opener", "browser-state");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{provider.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}.json");
    }
}

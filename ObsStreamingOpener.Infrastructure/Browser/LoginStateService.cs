using Microsoft.Extensions.Options;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Options;

namespace ObsStreamingOpener.Infrastructure.Browser;

public sealed class LoginStateService(IOptions<BrowserAutomationOptions> options) : ILoginStateService
{
    public Task<bool> HasStateAsync(ProviderKind provider, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(GetStatePath(provider)));

    public Task<string> GetStatePathAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.Value.AuthStateDirectory);
        return Task.FromResult(GetStatePath(provider));
    }

    public Task<BrowserLoginResultDto> ClearStateAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        var path = GetStatePath(provider);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.FromResult(new BrowserLoginResultDto(provider, "Cleared", "Browser storage state was removed.", path));
    }

    private string GetStatePath(ProviderKind provider)
    {
        var fileName = provider.ToString().ToLowerInvariant() switch
        {
            "zrzutka" => "zrzutka.json",
            var value => $"{value}.json"
        };
        return Path.Combine(options.Value.AuthStateDirectory, fileName);
    }
}

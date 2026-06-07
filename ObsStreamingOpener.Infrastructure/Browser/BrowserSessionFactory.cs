using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Options;

namespace ObsStreamingOpener.Infrastructure.Browser;

public sealed class BrowserSessionFactory(
    ILoginStateService loginStateService,
    IOptions<BrowserAutomationOptions> options) : IBrowserSessionFactory
{
    public async Task<BrowserLoginResultDto> StartManualLoginAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        var statePath = await loginStateService.GetStatePathAsync(provider, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = options.Value.SlowMo
        });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = File.Exists(statePath) ? statePath : null
        });
        context.SetDefaultTimeout(options.Value.DefaultTimeoutMilliseconds);
        context.SetDefaultNavigationTimeout(options.Value.NavigationTimeoutMilliseconds);
        var page = await context.NewPageAsync();
        await page.GotoAsync("about:blank");
        await context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = statePath });
        await context.CloseAsync();

        return new BrowserLoginResultDto(
            provider,
            "ManualLoginStarted",
            "A headful browser session was opened. Configure provider login URLs before using this endpoint for real login capture.",
            statePath);
    }
}

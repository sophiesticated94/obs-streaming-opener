using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Options;

namespace ObsStreamingOpener.Infrastructure.Browser;

public sealed class BrowserSessionFactory(
    ILoginStateService loginStateService,
    IOptions<BrowserAutomationOptions> options,
    IOptionsMonitor<SupportProviderOptions> supportProviderOptions) : IBrowserSessionFactory
{
    public async Task<BrowserLoginResultDto> StartManualLoginAsync(ProviderKind provider, CancellationToken cancellationToken = default)
    {
        var statePath = await loginStateService.GetStatePathAsync(provider, cancellationToken);
        try
        {
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
            await page.GotoAsync(ResolveLoginUrl(provider));

            while (!page.IsClosed)
            {
                await Task.Delay(1000, cancellationToken);
            }

            var storageStateJson = await context.StorageStateAsync();
            await loginStateService.SaveStateAsync(provider, storageStateJson, cancellationToken);
            await context.CloseAsync();

            return new BrowserLoginResultDto(
                provider,
                "Saved",
                "Browser login finished and encrypted storage state was saved.",
                null);
        }
        finally
        {
            await loginStateService.DeleteTemporaryStateAsync(statePath, cancellationToken);
        }
    }

    private string ResolveLoginUrl(ProviderKind provider)
    {
        var optionsForProvider = supportProviderOptions.Get(provider.ToString());
        if (!string.IsNullOrWhiteSpace(optionsForProvider.LoginUrl))
        {
            return optionsForProvider.LoginUrl;
        }

        if (!string.IsNullOrWhiteSpace(optionsForProvider.DashboardUrl))
        {
            return optionsForProvider.DashboardUrl;
        }

        return provider == ProviderKind.Tipply ? "https://tipply.pl/" : "about:blank";
    }
}

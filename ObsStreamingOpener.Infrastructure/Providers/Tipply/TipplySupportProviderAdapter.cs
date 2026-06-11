using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Exceptions;
using ObsStreamingOpener.Domain;
using ObsStreamingOpener.Infrastructure.Options;

namespace ObsStreamingOpener.Infrastructure.Providers.Tipply;

public sealed class TipplySupportProviderAdapter(
    ILoginStateService loginStateService,
    IChannelStore channelStore,
    IProviderCursorStore cursorStore,
    IOptionsMonitor<SupportProviderOptions> options,
    ILogger<TipplySupportProviderAdapter> logger) : ISupportProviderAdapter
{
    private const int PageLimit = 50;
    private const string CursorName = "tipply:last-tip-id";

    public ProviderKind Provider => ProviderKind.Tipply;

    public bool Enabled => options.Get(Provider.ToString()).Enabled;

    public async IAsyncEnumerable<ProviderTipRecord> GetTipsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await loginStateService.HasStateAsync(Provider, cancellationToken))
        {
            throw new AuthenticationRequiredException(Provider.ToString(), "Tipply browser session is missing or expired. Run browser login from the dashboard.");
        }

        var target = await ResolveTargetAsync(cancellationToken);
        var knownLastExternalId = target.Connection is null
            ? null
            : await cursorStore.GetCursorAsync(target.Connection.Id, CursorName, cancellationToken);
        var newestExternalId = default(string);
        var statePath = await loginStateService.GetStatePathAsync(Provider, cancellationToken);

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var request = await playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
            {
                BaseURL = ResolveBaseUrl(),
                StorageStatePath = File.Exists(statePath) ? statePath : null
            });

            var offset = 0;
            var stop = false;
            while (!stop)
            {
                var response = await request.GetAsync(ResolveTipsPath(PageLimit, offset));
                var body = await response.TextAsync();
                if (!response.Ok)
                {
                    await HandleFailureAsync(response, body, cancellationToken);
                }

                if (LooksLikeLoginPage(response.Url, body))
                {
                    await MarkNeedsLoginAsync(cancellationToken);
                }

                var tips = TipplyTipParser.ParseTips(body);
                if (tips.Count == 0)
                {
                    break;
                }

                foreach (var tip in tips)
                {
                    var externalId = tip.Id ?? tip.TipId ?? tip.TransactionId;
                    newestExternalId ??= externalId;
                    if (!string.IsNullOrWhiteSpace(knownLastExternalId)
                        && string.Equals(externalId, knownLastExternalId, StringComparison.Ordinal))
                    {
                        stop = true;
                        break;
                    }

                    yield return TipplyTipParser.ToProviderTip(target.MonitoredChannelId, tip, DateTimeOffset.UtcNow, body);
                }

                offset += PageLimit;
            }

            await request.DisposeAsync();
        }
        finally
        {
            await loginStateService.DeleteTemporaryStateAsync(statePath, cancellationToken);
        }

        if (target.Connection is not null && !string.IsNullOrWhiteSpace(newestExternalId))
        {
            await cursorStore.SetCursorAsync(target.Connection.Id, CursorName, newestExternalId, metadataJson: "{\"source\":\"tipply\"}", cancellationToken: cancellationToken);
        }
    }

    public async IAsyncEnumerable<ProviderPatronRecord> GetPatronsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    private async Task<TipplyTarget> ResolveTargetAsync(CancellationToken cancellationToken)
    {
        var connection = (await channelStore.GetEnabledConnectionsAsync(Provider, cancellationToken)).FirstOrDefault();
        if (connection is not null)
        {
            return new TipplyTarget(connection.MonitoredChannelId, connection);
        }

        var channel = await channelStore.GetDefaultChannelAsync(cancellationToken);
        return new TipplyTarget(channel.Id, null);
    }

    private string ResolveBaseUrl()
        => options.Get(Provider.ToString()).BaseUrl?.TrimEnd('/') ?? "https://proxy.tipply.pl";

    private string ResolveTipsPath(int limit, int offset)
    {
        var configured = options.Get(Provider.ToString()).DonationsUrl;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var separator = configured.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{configured}{separator}limit={limit}&offset={offset}&filter=undefined&search=undefined";
        }

        return $"/user/tips?limit={limit}&offset={offset}&filter=undefined&search=undefined";
    }

    private async Task HandleFailureAsync(IAPIResponse response, string body, CancellationToken cancellationToken)
    {
        if (response.Status is 401 or 403)
        {
            await MarkNeedsLoginAsync(cancellationToken);
        }

        logger.LogWarning("Tipply tips request failed with {Status}: {Body}", response.Status, body);
        throw new InvalidOperationException($"Tipply tips request failed with HTTP {response.Status}.");
    }

    private async Task MarkNeedsLoginAsync(CancellationToken cancellationToken)
    {
        await loginStateService.ClearStateAsync(Provider, cancellationToken);
        throw new AuthenticationRequiredException(Provider.ToString(), "Tipply browser session is not authenticated. Run browser login from the dashboard.");
    }

    private static bool LooksLikeLoginPage(string? url, string body)
        => (url?.Contains("login", StringComparison.OrdinalIgnoreCase) == true)
            || body.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || body.Contains("logowanie", StringComparison.OrdinalIgnoreCase)
            || body.Contains("sign in", StringComparison.OrdinalIgnoreCase);

    private sealed record TipplyTarget(Guid MonitoredChannelId, ProviderConnectionDto? Connection);
}

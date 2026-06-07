using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/revenue")]
public sealed class RevenueController(
    IRevenueCalculator revenueCalculator,
    IRevenueForecastService forecastService,
    IRevenueSynchronizer synchronizer,
    IBrowserSessionFactory browserSessionFactory,
    ILoginStateService loginStateService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] Guid? channelId,
        [FromQuery] DateTimeOffset? since,
        [FromQuery] DateTimeOffset? until,
        [FromQuery] ProviderKind? provider,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
        => Ok(await revenueCalculator.GetSummaryAsync(new RevenueSummaryQuery(channelId, since, until, provider, null, null, currency), cancellationToken));

    [HttpGet("stream/{streamSessionId:guid}")]
    public async Task<IActionResult> GetStreamRevenue(
        Guid streamSessionId,
        [FromQuery] DateTimeOffset? since,
        [FromQuery] DateTimeOffset? until,
        [FromQuery] ProviderKind? provider,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
        => Ok(await revenueCalculator.GetSummaryAsync(new RevenueSummaryQuery(null, since, until, provider, streamSessionId, null, currency), cancellationToken));

    [HttpGet("rankings")]
    public async Task<IActionResult> GetRankings(
        [FromQuery] Guid? channelId,
        [FromQuery] DateTimeOffset? since,
        [FromQuery] DateTimeOffset? until,
        [FromQuery] Guid? streamSessionId,
        [FromQuery] ProviderKind? provider,
        [FromQuery] string? currency,
        CancellationToken cancellationToken)
        => Ok(await revenueCalculator.GetRankingAsync(new RevenueSummaryQuery(channelId, since, until, provider, streamSessionId, null, currency), cancellationToken));

    [HttpGet("forecast")]
    public async Task<IActionResult> GetForecast([FromQuery] Guid? channelId, [FromQuery] int days = 7, CancellationToken cancellationToken = default)
        => Ok(await forecastService.GetForecastAsync(channelId, days, cancellationToken));

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken cancellationToken)
        => Ok(await synchronizer.SyncAsync(cancellationToken));

    [HttpGet("providers/status")]
    public async Task<IActionResult> GetProviderStatus(CancellationToken cancellationToken)
        => Ok(await synchronizer.GetProviderStatusesAsync(cancellationToken));

    [HttpPost("providers/{provider}/browser-login")]
    public async Task<IActionResult> StartBrowserLogin(ProviderKind provider, CancellationToken cancellationToken)
        => Ok(await browserSessionFactory.StartManualLoginAsync(provider, cancellationToken));

    [HttpDelete("providers/{provider}/browser-session")]
    public async Task<IActionResult> ClearBrowserSession(ProviderKind provider, CancellationToken cancellationToken)
        => Ok(await loginStateService.ClearStateAsync(provider, cancellationToken));
}

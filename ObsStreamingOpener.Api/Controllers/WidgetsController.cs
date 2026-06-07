using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/widgets")]
public sealed class WidgetsController(
    IStatsQueryService statsQueryService,
    IChannelStore channelStore,
    IProviderMessageStore providerMessageStore,
    IAlertService alertService) : ControllerBase
{
    [HttpGet("alerts/data")]
    public async Task<IActionResult> GetAlertsData([FromQuery] Guid? channelId, [FromQuery] Guid? streamSessionId, CancellationToken cancellationToken = default)
    {
        var effectiveChannelId = channelId ?? (await channelStore.GetDefaultChannelAsync(cancellationToken)).Id;
        return Ok(await alertService.GetWidgetDataAsync(effectiveChannelId, streamSessionId, cancellationToken));
    }

    [HttpGet("comment-explorer/data")]
    public async Task<IActionResult> GetCommentExplorerData(
        [FromQuery] Guid? channelId,
        [FromQuery] MessageSource? source,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var effectiveChannelId = channelId ?? (await channelStore.GetDefaultChannelAsync(cancellationToken)).Id;
        var messages = await providerMessageStore.GetRecentMessagesAsync(effectiveChannelId, source, limit, cancellationToken: cancellationToken);
        return Ok(new
        {
            channelId = effectiveChannelId,
            refreshedAt = DateTimeOffset.UtcNow,
            messages
        });
    }

    [HttpGet("{widgetKey}/data")]
    public async Task<IActionResult> GetData(string widgetKey, [FromQuery] Guid? channelId, CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetWidgetDataAsync(widgetKey, channelId, cancellationToken));
}

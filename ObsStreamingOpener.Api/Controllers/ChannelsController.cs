using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/channels")]
public sealed class ChannelsController(
    IChannelStore channelStore,
    IEventStore eventStore,
    IStatsQueryService statsQueryService,
    IAudienceStore audienceStore) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetChannels(CancellationToken cancellationToken)
        => Ok(await channelStore.GetChannelsAsync(cancellationToken));

    [HttpGet("{channelId:guid}")]
    public async Task<IActionResult> GetChannel(Guid channelId, CancellationToken cancellationToken)
    {
        var channel = await channelStore.GetChannelAsync(channelId, cancellationToken);
        return channel is null ? NotFound() : Ok(channel);
    }

    [HttpGet("{channelId:guid}/events/recent")]
    public async Task<IActionResult> GetRecentEvents(
        Guid channelId,
        [FromQuery] StreamEventType? type,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await eventStore.GetRecentEventsAsync(channelId, null, type, limit, cancellationToken));
    }

    [HttpGet("{channelId:guid}/stats/current")]
    public async Task<IActionResult> GetCurrentStats(Guid channelId, CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetCurrentStatsAsync(channelId, cancellationToken));

    [HttpGet("{channelId:guid}/stats/summary")]
    public async Task<IActionResult> GetStatsSummary(
        Guid channelId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetSummaryAsync(channelId, from, to, cancellationToken));

    [HttpGet("{channelId:guid}/audience/recent")]
    public async Task<IActionResult> GetRecentAudience(Guid channelId, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await audienceStore.GetRecentRelationshipsAsync(channelId, limit, cancellationToken));
    }

    [HttpGet("{channelId:guid}/audience/{audienceMemberId:guid}/history")]
    public async Task<IActionResult> GetAudienceHistory(Guid channelId, Guid audienceMemberId, CancellationToken cancellationToken)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await audienceStore.GetRelationshipHistoryAsync(channelId, audienceMemberId, cancellationToken));
    }
}

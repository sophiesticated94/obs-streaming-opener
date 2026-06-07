using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/channels")]
public sealed class ChannelsController(
    IChannelStore channelStore,
    IEventStore eventStore,
    IProviderMessageStore providerMessageStore,
    IAlertService alertService,
    IStatsStore statsStore,
    IStatsQueryService statsQueryService,
    IAudienceStore audienceStore,
    IContentQueryService contentQueryService) : ControllerBase
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
        [FromQuery] Guid? providerResourceId,
        [FromQuery] Guid? streamSessionId,
        [FromQuery] Guid? audienceMemberId,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await eventStore.GetRecentEventsAsync(channelId, null, type, limit, providerResourceId, streamSessionId, audienceMemberId, cancellationToken));
    }

    [HttpGet("{channelId:guid}/stream/current")]
    public async Task<IActionResult> GetCurrentStream(Guid channelId, CancellationToken cancellationToken)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        var stream = await statsStore.GetCurrentStreamAsync(channelId, cancellationToken);
        return stream is null ? NotFound() : Ok(stream);
    }

    [HttpGet("{channelId:guid}/alerts/active")]
    public async Task<IActionResult> GetActiveAlerts(Guid channelId, [FromQuery] Guid? streamSessionId, CancellationToken cancellationToken = default)
        => Ok(await alertService.GetActiveAlertsAsync(channelId, streamSessionId, cancellationToken));

    [HttpGet("{channelId:guid}/alerts/recent")]
    public async Task<IActionResult> GetRecentAlerts(Guid channelId, [FromQuery] Guid? streamSessionId, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
        => Ok(await alertService.GetRecentAlertsAsync(channelId, streamSessionId, limit, cancellationToken));

    [HttpGet("{channelId:guid}/events/alert-trace")]
    public async Task<IActionResult> GetEventAlertTrace(Guid channelId, [FromQuery] Guid? streamSessionId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
        => Ok(await alertService.GetEventAlertTraceAsync(channelId, streamSessionId, limit, cancellationToken));

    [HttpPost("{channelId:guid}/alerts/manual")]
    public async Task<IActionResult> CreateManualAlert(Guid channelId, [FromBody] ManualAlertRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await alertService.CreateManualAlertAsync(channelId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{channelId:guid}/alerts/{alertId:guid}/ack")]
    public async Task<IActionResult> AcknowledgeAlert(Guid channelId, Guid alertId, CancellationToken cancellationToken = default)
        => await alertService.AcknowledgeAlertAsync(channelId, alertId, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("{channelId:guid}/stats/current")]
    public async Task<IActionResult> GetCurrentStats(Guid channelId, CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetCurrentStatsAsync(channelId, cancellationToken));

    [HttpGet("{channelId:guid}/stats/summary")]
    public async Task<IActionResult> GetStatsSummary(
        Guid channelId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? providerResourceId,
        [FromQuery] Guid? streamSessionId,
        CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetSummaryAsync(channelId, from, to, providerResourceId, streamSessionId, cancellationToken));

    [HttpGet("{channelId:guid}/audience/recent")]
    public async Task<IActionResult> GetRecentAudience(Guid channelId, [FromQuery] int limit = 20, [FromQuery] bool includeRevenue = false, CancellationToken cancellationToken = default)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await audienceStore.GetRecentRelationshipsAsync(channelId, limit, includeRevenue, cancellationToken));
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

    [HttpGet("{channelId:guid}/audience/{audienceMemberId:guid}/activity")]
    public async Task<IActionResult> GetAudienceActivity(Guid channelId, Guid audienceMemberId, CancellationToken cancellationToken)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        var history = await audienceStore.GetRelationshipHistoryAsync(channelId, audienceMemberId, cancellationToken);
        var events = await eventStore.GetRecentEventsAsync(channelId, null, null, 100, audienceMemberId: audienceMemberId, cancellationToken: cancellationToken);
        var messages = await providerMessageStore.GetRecentMessagesAsync(channelId, null, 100, audienceMemberId: audienceMemberId, cancellationToken: cancellationToken);
        var revenue = await audienceStore.GetAudienceRevenueAsync(channelId, audienceMemberId, cancellationToken);
        return Ok(new AudienceActivityDto(audienceMemberId, history, events, messages, revenue));
    }

    [HttpGet("{channelId:guid}/content/recent")]
    public async Task<IActionResult> GetRecentContent(
        Guid channelId,
        [FromQuery] ProviderResourceKind? kind,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
        => Ok(await contentQueryService.GetRecentContentAsync(channelId, kind, limit, cancellationToken));

    [HttpGet("{channelId:guid}/content/{resourceId:guid}")]
    public async Task<IActionResult> GetContent(Guid channelId, Guid resourceId, CancellationToken cancellationToken)
    {
        var resource = await contentQueryService.GetContentAsync(channelId, resourceId, cancellationToken);
        return resource is null ? NotFound() : Ok(resource);
    }

    [HttpGet("{channelId:guid}/content/upcoming")]
    public async Task<IActionResult> GetUpcomingContent(Guid channelId, [FromQuery] int limit = 5, CancellationToken cancellationToken = default)
        => Ok(await contentQueryService.GetUpcomingContentAsync(channelId, limit, cancellationToken));

    [HttpGet("{channelId:guid}/comments/recent")]
    public async Task<IActionResult> GetRecentComments(Guid channelId, [FromQuery] int limit = 10, CancellationToken cancellationToken = default)
        => Ok(await contentQueryService.GetRecentCommentsAsync(channelId, limit, cancellationToken));

    [HttpGet("{channelId:guid}/messages/recent")]
    public async Task<IActionResult> GetRecentMessages(
        Guid channelId,
        [FromQuery] MessageSource? source,
        [FromQuery] Guid? providerResourceId,
        [FromQuery] Guid? streamSessionId,
        [FromQuery] Guid? audienceMemberId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await providerMessageStore.GetRecentMessagesAsync(channelId, source, limit, providerResourceId, streamSessionId, audienceMemberId, cancellationToken));
    }

    [HttpGet("{channelId:guid}/messages/search")]
    public async Task<IActionResult> SearchMessages(
        Guid channelId,
        [FromQuery] string? query,
        [FromQuery] MessageSource? source,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (await channelStore.GetChannelAsync(channelId, cancellationToken) is null)
        {
            return NotFound();
        }

        return Ok(await providerMessageStore.SearchMessagesAsync(channelId, query, source, from, to, limit, cancellationToken));
    }

    [HttpGet("{channelId:guid}/youtube/overview")]
    public async Task<IActionResult> GetYouTubeOverview(Guid channelId, CancellationToken cancellationToken)
        => Ok(await contentQueryService.GetYouTubeOverviewAsync(channelId, cancellationToken));
}

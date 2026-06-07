using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController(IEventStore eventStore, IChannelStore channelStore) : ControllerBase
{
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent(
        [FromQuery] Guid? channelId,
        [FromQuery] ProviderKind? provider,
        [FromQuery] StreamEventType? type,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var channel = channelId.HasValue
            ? await channelStore.GetChannelAsync(channelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);
        if (channel is null)
        {
            return NotFound();
        }

        var events = await eventStore.GetRecentEventsAsync(channel.Id, provider, type, limit, cancellationToken: cancellationToken);
        return Ok(events);
    }
}

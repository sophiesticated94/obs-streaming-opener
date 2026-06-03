using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController(IEventStore eventStore) : ControllerBase
{
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent(
        [FromQuery] ProviderKind? provider,
        [FromQuery] StreamEventType? type,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var events = await eventStore.GetRecentEventsAsync(provider, type, limit, cancellationToken);
        return Ok(events);
    }
}

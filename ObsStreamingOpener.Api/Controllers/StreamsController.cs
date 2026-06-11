using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/streams")]
public sealed class StreamsController(IStatsStore statsStore) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent([FromQuery] Guid? channelId, [FromServices] IChannelStore channelStore, CancellationToken cancellationToken)
    {
        var channel = channelId.HasValue
            ? await channelStore.GetChannelAsync(channelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);
        if (channel is null)
        {
            return NotFound();
        }

        var stream = await statsStore.GetCurrentStreamAsync(channel.Id, cancellationToken);
        return new JsonResult(stream);
    }
}

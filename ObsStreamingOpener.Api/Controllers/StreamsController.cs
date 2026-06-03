using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/streams")]
public sealed class StreamsController(IStatsStore statsStore) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var stream = await statsStore.GetCurrentStreamAsync(cancellationToken);
        return stream is null ? NotFound() : Ok(stream);
    }
}

using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/stats")]
public sealed class StatsController(IStatsQueryService statsQueryService) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetCurrentStatsAsync(cancellationToken));

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetSummaryAsync(from, to, cancellationToken));
}

using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/widgets")]
public sealed class WidgetsController(IStatsQueryService statsQueryService) : ControllerBase
{
    [HttpGet("{widgetKey}/data")]
    public async Task<IActionResult> GetData(string widgetKey, CancellationToken cancellationToken)
        => Ok(await statsQueryService.GetWidgetDataAsync(widgetKey, cancellationToken));
}

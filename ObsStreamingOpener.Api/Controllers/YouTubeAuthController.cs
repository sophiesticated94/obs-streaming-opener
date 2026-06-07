using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/auth/youtube")]
public sealed class YouTubeAuthController(IYouTubeOAuthService youtubeOAuthService) : ControllerBase
{
    [HttpGet("start")]
    public IActionResult Start([FromQuery] Guid? accountId)
        => Execute(() => Ok(youtubeOAuthService.Start(accountId)));

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken cancellationToken)
        => await ExecuteAsync(async () =>
        {
            await youtubeOAuthService.CompleteCallbackAsync(code, state, cancellationToken);
            return Redirect("/dashboard/accounts?connected=success");
        });

    [HttpPost("relogin/{accountId:guid}")]
    public async Task<IActionResult> Relogin(Guid accountId, CancellationToken cancellationToken)
        => await ExecuteAsync(async () => Ok(await youtubeOAuthService.ReloginAsync(accountId, cancellationToken)));

    [HttpPost("refresh/{accountId:guid}")]
    public async Task<IActionResult> Refresh(Guid accountId, CancellationToken cancellationToken)
        => await ExecuteAsync(async () =>
        {
            var account = await youtubeOAuthService.RefreshAsync(accountId, cancellationToken);
            return account is null ? NotFound() : Ok(account);
        });

    [HttpPost("sync/{accountId:guid}")]
    public async Task<IActionResult> Sync(Guid accountId, CancellationToken cancellationToken)
        => await ExecuteAsync(async () =>
        {
            var account = await youtubeOAuthService.SyncAsync(accountId, cancellationToken);
            return account is null ? NotFound() : Ok(account);
        });

    [HttpDelete("disconnect/{accountId:guid}")]
    public async Task<IActionResult> Disconnect(Guid accountId, CancellationToken cancellationToken)
        => await youtubeOAuthService.DisconnectAsync(accountId, cancellationToken) ? NoContent() : NotFound();

    private static IActionResult Execute(Func<IActionResult> action)
    {
        try
        {
            return action();
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }

    private static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }
}

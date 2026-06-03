using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(IChannelStore channelStore) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
        => Ok(await channelStore.GetAccountsAsync(cancellationToken));
}

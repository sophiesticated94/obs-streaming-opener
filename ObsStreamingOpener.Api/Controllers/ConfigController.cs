using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/config")]
public sealed class ConfigController(IConfigurationService configurationService) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
        => Ok(await configurationService.GetAccountsAsync(cancellationToken));

    [HttpGet("accounts/connected")]
    public async Task<IActionResult> GetConnectedAccounts(CancellationToken cancellationToken)
        => Ok(await configurationService.GetConnectedAccountsAsync(cancellationToken));

    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] SaveAccountRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(async () => Ok(await configurationService.CreateAccountAsync(request, cancellationToken)));

    [HttpPut("accounts/{accountId:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid accountId, [FromBody] SaveAccountRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(async () =>
        {
            var account = await configurationService.UpdateAccountAsync(accountId, request, cancellationToken);
            return account is null ? NotFound() : Ok(account);
        });

    [HttpGet("channels")]
    public async Task<IActionResult> GetChannels(CancellationToken cancellationToken)
        => Ok(await configurationService.GetChannelsAsync(cancellationToken));

    [HttpPut("channels/{channelId:guid}")]
    public async Task<IActionResult> UpdateChannel(Guid channelId, [FromBody] SaveChannelSettingsRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(async () =>
        {
            var channel = await configurationService.UpdateChannelAsync(channelId, request, cancellationToken);
            return channel is null ? NotFound() : Ok(channel);
        });

    [HttpGet("provider-connections")]
    public async Task<IActionResult> GetProviderConnections([FromQuery] Guid? channelId, CancellationToken cancellationToken)
        => Ok(await configurationService.GetProviderConnectionsAsync(channelId, cancellationToken));

    [HttpPost("provider-connections")]
    public async Task<IActionResult> CreateProviderConnection([FromBody] SaveProviderConnectionRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(async () => Ok(await configurationService.CreateProviderConnectionAsync(request, cancellationToken)));

    [HttpPut("provider-connections/{providerConnectionId:guid}")]
    public async Task<IActionResult> UpdateProviderConnection(Guid providerConnectionId, [FromBody] SaveProviderConnectionRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(async () =>
        {
            var connection = await configurationService.UpdateProviderConnectionAsync(providerConnectionId, request, cancellationToken);
            return connection is null ? NotFound() : Ok(connection);
        });

    [HttpDelete("provider-connections/{providerConnectionId:guid}")]
    public async Task<IActionResult> DeleteProviderConnection(Guid providerConnectionId, CancellationToken cancellationToken)
        => await configurationService.DeleteProviderConnectionAsync(providerConnectionId, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("widgets")]
    public async Task<IActionResult> GetWidgets(CancellationToken cancellationToken)
        => Ok(await configurationService.GetWidgetConfigurationsAsync(cancellationToken));

    [HttpPut("widgets")]
    public async Task<IActionResult> UpsertWidget([FromBody] SaveWidgetConfigurationRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(async () => Ok(await configurationService.UpsertWidgetConfigurationAsync(request, cancellationToken)));

    [HttpGet("alert-rules")]
    public async Task<IActionResult> GetAlertRules([FromQuery] Guid? channelId, CancellationToken cancellationToken)
        => Ok(await configurationService.GetAlertRulesAsync(channelId, cancellationToken));

    [HttpPut("alert-rules")]
    public async Task<IActionResult> UpsertAlertRule([FromBody] SaveAlertRuleRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(async () => Ok(await configurationService.UpsertAlertRuleAsync(request, cancellationToken)));

    [HttpGet("polling")]
    public IActionResult GetPolling()
        => Ok(configurationService.GetPollingConfiguration());

    private static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
    }
}

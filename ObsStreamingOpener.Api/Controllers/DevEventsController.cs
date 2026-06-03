using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/dev/events")]
public sealed class DevEventsController(
    IWebHostEnvironment environment,
    IStreamSessionStore streamSessionStore,
    IEventIngestionService ingestionService,
    IClock clock) : ControllerBase
{
    [HttpPost("sample")]
    public async Task<IActionResult> CreateSample([FromBody] SampleEventRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var session = await streamSessionStore.GetOrCreateCurrentSessionAsync(cancellationToken);
        var result = await ingestionService.IngestAsync(new ProviderEvent(
            session.Id,
            request.Provider,
            request.EventType,
            request.ExternalEventId ?? Guid.NewGuid().ToString("N"),
            request.ActorName ?? "Sample viewer",
            null,
            request.Title ?? "Sample event",
            request.Message ?? "Hello from the development event endpoint",
            request.Amount,
            request.Currency,
            clock.UtcNow,
            "{\"source\":\"development\"}"), cancellationToken);

        return Ok(result);
    }
}

public sealed record SampleEventRequest(
    ProviderKind Provider = ProviderKind.Custom,
    StreamEventType EventType = StreamEventType.ChatMessage,
    string? ExternalEventId = null,
    string? ActorName = null,
    string? Title = null,
    string? Message = null,
    decimal? Amount = null,
    string? Currency = null);

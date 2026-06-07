using Microsoft.AspNetCore.Mvc;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Api.Controllers;

[ApiController]
[Route("api/dev/events")]
public sealed class DevEventsController(
    IWebHostEnvironment environment,
    IChannelStore channelStore,
    IEventIngestionService ingestionService,
    IAudienceIngestionService audienceIngestionService,
    IClock clock) : ControllerBase
{
    [HttpPost("sample")]
    public async Task<IActionResult> CreateSample([FromBody] SampleEventRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var channel = request.ChannelId.HasValue
            ? await channelStore.GetChannelAsync(request.ChannelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);
        if (channel is null)
        {
            return NotFound();
        }

        var result = await ingestionService.IngestAsync(new ProviderEvent(
            channel.Id,
            request.StreamSessionId,
            request.AudienceMemberId,
            null,
            request.Provider,
            request.EventType,
            request.ExternalEventId ?? Guid.NewGuid().ToString(),
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

    [HttpPost("audience/sample")]
    public async Task<IActionResult> CreateAudienceSample([FromBody] SampleAudienceRelationshipRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var channel = request.ChannelId.HasValue
            ? await channelStore.GetChannelAsync(request.ChannelId.Value, cancellationToken)
            : await channelStore.GetDefaultChannelAsync(cancellationToken);
        if (channel is null)
        {
            return NotFound();
        }

        var result = await audienceIngestionService.IngestRelationshipAsync(new ProviderAudienceRelationship(
            channel.Id,
            request.Provider,
            request.ExternalAudienceId ?? Guid.NewGuid().ToString("N"),
            request.DisplayName ?? "Sample audience",
            request.ProfileUrl,
            request.RelationshipKind,
            request.StartedAt ?? clock.UtcNow,
            request.IsEstimated,
            "{\"source\":\"development\"}"), cancellationToken);

        return Ok(result);
    }
}

public sealed record SampleEventRequest(
    Guid? ChannelId = null,
    Guid? StreamSessionId = null,
    Guid? AudienceMemberId = null,
    ProviderKind Provider = ProviderKind.Custom,
    StreamEventType EventType = StreamEventType.ChatMessage,
    string? ExternalEventId = null,
    string? ActorName = null,
    string? Title = null,
    string? Message = null,
    decimal? Amount = null,
    string? Currency = null);

public sealed record SampleAudienceRelationshipRequest(
    Guid? ChannelId = null,
    ProviderKind Provider = ProviderKind.Custom,
    string? ExternalAudienceId = null,
    string? DisplayName = null,
    string? ProfileUrl = null,
    AudienceRelationshipKind RelationshipKind = AudienceRelationshipKind.Free,
    DateTimeOffset? StartedAt = null,
    bool IsEstimated = false);

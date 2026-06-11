using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class AlertService(
    IAlertRuleStore ruleStore,
    IStreamAlertStore alertStore,
    IStreamSessionStore sessionStore,
    IClock clock,
    IConfigurationService? configurationService = null,
    IAlertPublisher? alertPublisher = null) : IAlertService
{
    public async Task CreateAlertForEventAsync(StreamEvent streamEvent, IngestedEventResult result, CancellationToken cancellationToken = default)
    {
        if (!result.Stored || result.EventId is null || streamEvent.StreamSessionId is null)
        {
            return;
        }

        var rule = await ruleStore.GetAlertRuleAsync(streamEvent.MonitoredChannelId, streamEvent.EventType, cancellationToken)
            ?? GetDefaultRule(streamEvent.MonitoredChannelId, streamEvent.EventType);
        if (rule is null || !rule.Enabled)
        {
            return;
        }

        if (streamEvent.EventType == StreamEventType.Tip
            && rule.MinimumAmount.HasValue
            && (!streamEvent.Value.HasValue || streamEvent.Value.Value < rule.MinimumAmount.Value))
        {
            return;
        }

        var now = clock.UtcNow;
        var alert = await alertStore.AddAlertAsync(new StreamAlert
        {
            MonitoredChannelId = streamEvent.MonitoredChannelId,
            StreamSessionId = streamEvent.StreamSessionId.Value,
            StreamEventId = result.EventId,
            AlertType = ToAlertType(streamEvent.EventType),
            Provider = streamEvent.Provider,
            IsSystemAlert = false,
            Title = Render(rule.TitleTemplate, streamEvent) ?? DefaultTitle(streamEvent),
            Message = Render(rule.MessageTemplate, streamEvent) ?? streamEvent.Message,
            ActorName = streamEvent.ActorName,
            Amount = streamEvent.Value,
            Currency = streamEvent.Unit,
            VisualStyle = rule.VisualStyle,
            MediaUrl = rule.MediaUrl,
            SoundUrl = rule.SoundUrl,
            PayloadJson = streamEvent.ContextJson ?? "{}",
            DisplayFromUtc = now,
            DisplayUntilUtc = now.AddSeconds(Math.Clamp(rule.DurationSeconds, 1, 60)),
            CreatedAtUtc = now
        }, cancellationToken);
        if (alertPublisher is not null)
        {
            await alertPublisher.PublishAlertAsync(alert, cancellationToken);
        }
    }

    public async Task<StreamAlertDto> CreateManualAlertAsync(Guid monitoredChannelId, ManualAlertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title is required.", nameof(request));
        }

        var sessionId = request.StreamSessionId;
        if (!sessionId.HasValue)
        {
            var current = await sessionStore.GetCurrentSessionAsync(monitoredChannelId, cancellationToken);
            sessionId = current?.Id;
        }

        if (!sessionId.HasValue)
        {
            throw new InvalidOperationException("Manual alerts require an active stream session or explicit streamSessionId.");
        }

        var now = clock.UtcNow;
        var durationSeconds = Math.Clamp(request.DurationSeconds ?? 6, 1, 60);
        var alert = await alertStore.AddAlertAsync(new StreamAlert
        {
            MonitoredChannelId = monitoredChannelId,
            StreamSessionId = sessionId.Value,
            Provider = ProviderKind.Custom,
            AlertType = AlertType.System,
            IsSystemAlert = true,
            Title = request.Title.Trim(),
            Message = string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim(),
            VisualStyle = string.IsNullOrWhiteSpace(request.VisualStyle) ? "system" : request.VisualStyle.Trim(),
            MediaUrl = string.IsNullOrWhiteSpace(request.MediaUrl) ? null : request.MediaUrl.Trim(),
            SoundUrl = string.IsNullOrWhiteSpace(request.SoundUrl) ? null : request.SoundUrl.Trim(),
            PayloadJson = "{\"source\":\"manual\"}",
            DisplayFromUtc = now,
            DisplayUntilUtc = now.AddSeconds(durationSeconds),
            CreatedAtUtc = now
        }, cancellationToken);
        if (alertPublisher is not null)
        {
            await alertPublisher.PublishAlertAsync(alert, cancellationToken);
        }

        return alert;
    }

    public async Task<IReadOnlyList<StreamAlertDto>> GetActiveAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId = null, CancellationToken cancellationToken = default)
    {
        var effectiveSessionId = streamSessionId ?? (await sessionStore.GetCurrentSessionAsync(monitoredChannelId, cancellationToken))?.Id;
        return await alertStore.GetActiveAlertsAsync(monitoredChannelId, effectiveSessionId, clock.UtcNow, cancellationToken);
    }

    public Task<IReadOnlyList<StreamAlertDto>> GetRecentAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default)
        => alertStore.GetRecentAlertsAsync(monitoredChannelId, streamSessionId, limit, cancellationToken);

    public Task<bool> AcknowledgeAlertAsync(Guid monitoredChannelId, Guid alertId, CancellationToken cancellationToken = default)
        => alertStore.AcknowledgeAlertAsync(monitoredChannelId, alertId, clock.UtcNow, cancellationToken);

    public Task<IReadOnlyList<StreamEventAlertTraceDto>> GetEventAlertTraceAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default)
        => alertStore.GetEventAlertTraceAsync(monitoredChannelId, streamSessionId, limit, cancellationToken);

    public Task<IReadOnlyList<StreamEventAlertTraceDto>> GetEventAlertTraceAsync(Guid monitoredChannelId, Guid? streamSessionId, Guid? providerResourceId, int limit, CancellationToken cancellationToken = default)
        => alertStore.GetEventAlertTraceAsync(monitoredChannelId, streamSessionId, providerResourceId, limit, cancellationToken);

    public async Task<AlertWidgetDataDto> GetWidgetDataAsync(Guid monitoredChannelId, Guid? streamSessionId, CancellationToken cancellationToken = default)
    {
        var currentSessionId = streamSessionId ?? (await sessionStore.GetCurrentSessionAsync(monitoredChannelId, cancellationToken))?.Id;
        var now = clock.UtcNow;
        var alerts = await alertStore.GetWidgetCandidateAlertsAsync(
            monitoredChannelId,
            currentSessionId,
            now.AddMinutes(-5),
            now.AddMinutes(5),
            cancellationToken);
        var settings = configurationService is null
            ? new AlertWidgetSettingsDto("default", "shortest-first", 1000, 60000, null, null, "sparkles", 0.8m, true)
            : await configurationService.GetAlertWidgetSettingsAsync(cancellationToken);
        return new AlertWidgetDataDto(monitoredChannelId, currentSessionId, now, alerts, settings);
    }

    private static AlertRuleDto? GetDefaultRule(Guid channelId, StreamEventType eventType)
    {
        var (enabled, duration, style, title, message, min) = eventType switch
        {
            StreamEventType.Tip => (true, 8, "tip", "{actor} tipped {amount} {currency}", "{message}", (decimal?)null),
            StreamEventType.CommentCreated => (true, 5, "comment", "New comment from {actor}", "{message}", null),
            StreamEventType.AudienceRelationshipStarted => (true, 7, "audience", "{actor} joined", "Welcome to the channel", null),
            StreamEventType.AudienceRelationshipRenewed => (true, 7, "audience", "{actor} renewed", "Thanks for staying with us", null),
            _ => (false, 6, "default", null, null, null)
        };

        return enabled
            ? new AlertRuleDto(Guid.Empty, channelId, eventType, true, min, duration, style, title, message, null, null, DateTimeOffset.MinValue)
            : null;
    }

    private static AlertType ToAlertType(StreamEventType eventType)
        => eventType switch
        {
            StreamEventType.Tip => AlertType.Tip,
            StreamEventType.CommentCreated or StreamEventType.ChatMessage => AlertType.Comment,
            StreamEventType.AudienceRelationshipStarted or StreamEventType.AudienceRelationshipRenewed or StreamEventType.AudienceRelationshipEnded => AlertType.Audience,
            _ => AlertType.System
        };

    private static string DefaultTitle(StreamEvent streamEvent)
        => streamEvent.EventType switch
        {
            StreamEventType.Tip => $"{streamEvent.ActorName ?? "Someone"} tipped",
            StreamEventType.CommentCreated => $"New comment from {streamEvent.ActorName ?? "viewer"}",
            StreamEventType.AudienceRelationshipStarted => $"{streamEvent.ActorName ?? "Someone"} joined",
            StreamEventType.AudienceRelationshipRenewed => $"{streamEvent.ActorName ?? "Someone"} renewed",
            _ => streamEvent.Title ?? streamEvent.EventType.ToString()
        };

    private static string? Render(string? template, StreamEvent streamEvent)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        return template
            .Replace("{actor}", streamEvent.ActorName ?? "Someone", StringComparison.OrdinalIgnoreCase)
            .Replace("{title}", streamEvent.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{message}", streamEvent.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{amount}", streamEvent.Value?.ToString("0.##") ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{currency}", streamEvent.Unit ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}

using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record StreamAlertDto(
    Guid Id,
    Guid MonitoredChannelId,
    Guid StreamSessionId,
    Guid? StreamEventId,
    AlertType AlertType,
    ProviderKind Provider,
    bool IsSystemAlert,
    string Title,
    string? Message,
    string? ActorName,
    decimal? Amount,
    string? Currency,
    string VisualStyle,
    string? MediaUrl,
    string? SoundUrl,
    DateTimeOffset DisplayFromUtc,
    DateTimeOffset DisplayUntilUtc,
    DateTimeOffset? AcknowledgedAtUtc,
    DateTimeOffset CreatedAtUtc,
    StreamEventType? SourceEventType = null,
    string? SourceEventTitle = null,
    string? SourceEventMessage = null,
    DateTimeOffset? SourceEventOccurredAt = null);

public sealed record StreamEventAlertTraceDto(
    Guid EventId,
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    ProviderKind Provider,
    StreamEventType EventType,
    string? ActorName,
    string? Title,
    string? Message,
    decimal? Amount,
    string? Currency,
    DateTimeOffset OccurredAt,
    Guid? AlertId,
    string AlertStatus);

public sealed record AlertRuleDto(
    Guid Id,
    Guid MonitoredChannelId,
    StreamEventType EventType,
    bool Enabled,
    decimal? MinimumAmount,
    int DurationSeconds,
    string VisualStyle,
    string? TitleTemplate,
    string? MessageTemplate,
    string? MediaUrl,
    string? SoundUrl,
    DateTimeOffset UpdatedAtUtc);

public sealed record SaveAlertRuleRequest(
    Guid MonitoredChannelId,
    StreamEventType EventType,
    bool Enabled,
    decimal? MinimumAmount,
    int DurationSeconds,
    string VisualStyle,
    string? TitleTemplate,
    string? MessageTemplate,
    string? MediaUrl,
    string? SoundUrl);

public sealed record ManualAlertRequest(
    Guid? StreamSessionId,
    string Title,
    string? Message,
    string? VisualStyle,
    int? DurationSeconds,
    string? MediaUrl,
    string? SoundUrl);

public sealed record AlertWidgetDataDto(
    Guid MonitoredChannelId,
    Guid? StreamSessionId,
    DateTimeOffset RefreshedAt,
    IReadOnlyList<StreamAlertDto> Alerts);

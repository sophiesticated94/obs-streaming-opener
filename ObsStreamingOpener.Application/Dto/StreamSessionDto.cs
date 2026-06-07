using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record StreamSessionDto(
    Guid Id,
    Guid MonitoredChannelId,
    string Title,
    bool IsActive,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    ProviderKind? Provider = null,
    string? ExternalSessionId = null,
    string? ExternalStreamId = null,
    string? ExternalLiveChatId = null,
    Guid? ProviderResourceId = null,
    DateTimeOffset? ScheduledStartAt = null,
    DateTimeOffset? ActualStartAt = null,
    DateTimeOffset? ActualEndAt = null);

public sealed record ProviderStreamSession(
    Guid MonitoredChannelId,
    ProviderKind Provider,
    string ExternalSessionId,
    string Title,
    string? ExternalStreamId,
    string? ExternalLiveChatId,
    Guid? ProviderResourceId,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    string? Status,
    string? PayloadSummaryJson);

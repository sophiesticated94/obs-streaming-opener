using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderResourceUpsert(
    Guid MonitoredChannelId,
    ProviderKind Provider,
    ProviderResourceKind ResourceKind,
    string ExternalResourceId,
    string? Title,
    string? Description,
    string? Url,
    string? Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    string? RawPayloadJson);

public sealed record ProviderResourceDto(
    Guid Id,
    Guid MonitoredChannelId,
    ProviderKind Provider,
    ProviderResourceKind ResourceKind,
    IReadOnlyList<ProviderResourceKind> ObservedKinds,
    string ExternalResourceId,
    string? Title,
    string? Description,
    string? Url,
    string? Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    DateTimeOffset LastSyncedAt,
    IReadOnlyList<ProviderResourcePatchDto> PatchHistory);

public sealed record ProviderResourcePatchDto(
    DateTimeOffset CapturedAtUtc,
    string Source,
    IReadOnlyList<ProviderResourcePatchFieldDto> Fields);

public sealed record ProviderResourcePatchFieldDto(
    string Field,
    string? OldValue,
    string? NewValue);

public sealed record ChannelContentOverviewDto(
    Guid MonitoredChannelId,
    ProviderResourceDto? LatestContent,
    ProviderResourceDto? NextUpcomingStream,
    IReadOnlyList<ProviderResourceDto> RecentContent,
    IReadOnlyList<RecentEventDto> RecentComments,
    DateTimeOffset GeneratedAt);

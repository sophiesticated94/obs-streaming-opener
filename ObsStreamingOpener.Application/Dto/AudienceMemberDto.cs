using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Dto;

public sealed record AudienceMemberDto(
    Guid Id,
    ProviderKind Provider,
    string ExternalAudienceId,
    string? DisplayName,
    string? ProfileUrl,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);

namespace ObsStreamingOpener.Application.Dto;

public sealed record StreamSessionDto(Guid Id, string Title, bool IsActive, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);

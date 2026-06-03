namespace ObsStreamingOpener.Application.Dto;

public sealed record IngestedEventResult(Guid? EventId, bool Stored, bool Duplicate);

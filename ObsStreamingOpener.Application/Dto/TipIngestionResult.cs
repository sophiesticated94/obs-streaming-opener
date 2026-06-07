namespace ObsStreamingOpener.Application.Dto;

public sealed record TipIngestionResult(Guid? StreamEventId, Guid? TipId, bool Stored, bool Duplicate);

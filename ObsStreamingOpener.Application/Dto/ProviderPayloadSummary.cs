namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderPayloadSummary(
    string Source,
    string? ProviderObjectId,
    string ObjectType,
    string? ResourceId,
    string? Status,
    string? Cursor,
    IReadOnlyDictionary<string, string?> ImportantFields);

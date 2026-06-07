namespace ObsStreamingOpener.Domain;

public sealed record FeeLine(
    FeeKind Kind,
    FeeSource Source,
    decimal Amount,
    string Currency,
    string? Description = null);

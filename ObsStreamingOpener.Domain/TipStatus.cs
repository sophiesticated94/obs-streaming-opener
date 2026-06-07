namespace ObsStreamingOpener.Domain;

public enum TipStatus
{
    Pending = 1,
    Settled = 2,
    Failed = 3,
    Refunded = 4,
    Reversed = 5,
    Unknown = 100
}

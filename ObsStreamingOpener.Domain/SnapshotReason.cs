namespace ObsStreamingOpener.Domain;

public enum SnapshotReason
{
    ScheduledPoll = 1,
    StreamStarted = 2,
    StreamEnded = 3,
    Manual = 4,
    ProviderEvent = 5
}

namespace ObsStreamingOpener.Application.Options;

public sealed class StreamingMonitorOptions
{
    public bool EnableStreamDataPolling { get; set; }

    public int StreamDataPollingSeconds { get; set; } = 5;
}

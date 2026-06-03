namespace ObsStreamingOpener.Application.Options;

public sealed class StreamingMonitorOptions
{
    public bool EnableYouTubePolling { get; set; }

    public int YouTubeMetricPollingSeconds { get; set; } = 10;
}

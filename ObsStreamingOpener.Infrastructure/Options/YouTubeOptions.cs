namespace ObsStreamingOpener.Infrastructure.Options;

public sealed class YouTubeOptions
{
    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://www.googleapis.com/youtube/v3/";
}

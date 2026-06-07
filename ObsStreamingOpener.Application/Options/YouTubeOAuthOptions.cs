namespace ObsStreamingOpener.Application.Options;

public sealed class YouTubeOAuthOptions
{
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string RedirectUri { get; set; } = "http://localhost:5198/api/auth/youtube/callback";
}

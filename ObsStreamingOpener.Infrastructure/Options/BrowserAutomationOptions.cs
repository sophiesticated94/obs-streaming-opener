namespace ObsStreamingOpener.Infrastructure.Options;

public sealed class BrowserAutomationOptions
{
    public bool Headless { get; set; } = true;

    public int SlowMo { get; set; }

    public string AuthStateDirectory { get; set; } = "playwright/.auth";

    public int DefaultTimeoutMilliseconds { get; set; } = 30_000;

    public int NavigationTimeoutMilliseconds { get; set; } = 30_000;

    public bool DebugArtifactsEnabled { get; set; }
}

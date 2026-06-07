namespace ObsStreamingOpener.Infrastructure.Options;

public sealed class SupportProviderOptions
{
    public bool Enabled { get; set; }

    public string? BaseUrl { get; set; }

    public string? LoginUrl { get; set; }

    public string? DashboardUrl { get; set; }

    public string? DonationsUrl { get; set; }

    public string? PayoutsUrl { get; set; }

    public string? PatronsUrl { get; set; }

    public string? ApiToken { get; set; }
}

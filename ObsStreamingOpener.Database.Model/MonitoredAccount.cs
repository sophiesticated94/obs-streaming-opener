using System.ComponentModel.DataAnnotations;

namespace ObsStreamingOpener.Database.Model;

public sealed class MonitoredAccount
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = "Default account";

    public bool IsDefault { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MonitoredChannel> Channels { get; set; } = new List<MonitoredChannel>();

    public ICollection<ProviderCredential> ProviderCredentials { get; set; } = new List<ProviderCredential>();
}

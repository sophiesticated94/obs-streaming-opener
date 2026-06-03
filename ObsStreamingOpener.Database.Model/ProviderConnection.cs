using System.ComponentModel.DataAnnotations;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class ProviderConnection
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    [MaxLength(256)]
    public string ExternalChannelId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ExternalStreamId { get; set; }

    [MaxLength(256)]
    public string? DisplayName { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MonitoredChannel? MonitoredChannel { get; set; }

    public ICollection<ProviderCursor> Cursors { get; set; } = new List<ProviderCursor>();
}

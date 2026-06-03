using System.ComponentModel.DataAnnotations;

namespace ObsStreamingOpener.Database.Model;

public sealed class StreamSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = "Current stream";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; set; }

    public ICollection<ProviderConnection> ProviderConnections { get; set; } = new List<ProviderConnection>();

    public ICollection<StreamEvent> Events { get; set; } = new List<StreamEvent>();

    public ICollection<MetricSnapshot> MetricSnapshots { get; set; } = new List<MetricSnapshot>();
}

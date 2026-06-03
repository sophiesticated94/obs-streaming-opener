using System.ComponentModel.DataAnnotations;

namespace ObsStreamingOpener.Database.Model;

public sealed class StreamSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = "Current stream";

    [MaxLength(256)]
    public string? ExternalStreamId { get; set; }

    [MaxLength(256)]
    public string? ExternalLiveChatId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; set; }

    public MonitoredChannel? MonitoredChannel { get; set; }

    public ICollection<StreamEvent> Events { get; set; } = new List<StreamEvent>();

    public ICollection<MetricSnapshot> MetricSnapshots { get; set; } = new List<MetricSnapshot>();
}

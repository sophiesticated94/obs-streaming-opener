using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class StreamAlert
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    [Required]
    public Guid StreamSessionId { get; set; }

    public Guid? StreamEventId { get; set; }

    [Required]
    public AlertType AlertType { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    public bool IsSystemAlert { get; set; }

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Message { get; set; }

    [MaxLength(256)]
    public string? ActorName { get; set; }

    public decimal? Amount { get; set; }

    [MaxLength(8)]
    public string? Currency { get; set; }

    [MaxLength(64)]
    public string VisualStyle { get; set; } = "default";

    [MaxLength(1024)]
    public string? MediaUrl { get; set; }

    [MaxLength(1024)]
    public string? SoundUrl { get; set; }

    [Column(TypeName = "TEXT")]
    public string PayloadJson { get; set; } = "{}";

    [Required]
    public DateTimeOffset DisplayFromUtc { get; set; }

    [Required]
    public DateTimeOffset DisplayUntilUtc { get; set; }

    public DateTimeOffset? AcknowledgedAtUtc { get; set; }

    [Required]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }

    [ForeignKey(nameof(StreamSessionId))]
    public StreamSession? StreamSession { get; set; }

    [ForeignKey(nameof(StreamEventId))]
    public StreamEvent? StreamEvent { get; set; }
}

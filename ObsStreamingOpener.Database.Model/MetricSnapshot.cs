using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class MetricSnapshot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    public Guid? StreamSessionId { get; set; }

    public Guid? ProviderConnectionId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    public MetricKind Metric { get; set; }

    [Required]
    public SnapshotReason SnapshotReason { get; set; } = SnapshotReason.ScheduledPoll;

    [Required]
    public decimal Value { get; set; }

    [MaxLength(32)]
    public string? Unit { get; set; }

    [Required]
    public DateTimeOffset CapturedAt { get; set; }

    [Column(TypeName = "TEXT")]
    public string? RawPayloadJson { get; set; }

    [ForeignKey(nameof(StreamSessionId))]
    public StreamSession? StreamSession { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }

    [ForeignKey(nameof(ProviderConnectionId))]
    public ProviderConnection? ProviderConnection { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class ProviderResource
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    public ProviderResourceKind ResourceKind { get; set; }

    [Required]
    [MaxLength(256)]
    public string ExternalResourceId { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Title { get; set; }

    [MaxLength(4096)]
    public string? Description { get; set; }

    [MaxLength(1024)]
    public string? Url { get; set; }

    [MaxLength(1024)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(128)]
    public string? Status { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? ScheduledStartAt { get; set; }

    public DateTimeOffset? ActualStartAt { get; set; }

    public DateTimeOffset? ActualEndAt { get; set; }

    public int? DurationSeconds { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "TEXT")]
    public string? RawPayloadJson { get; set; }

    [Column(TypeName = "TEXT")]
    public string? ObservedKindsJson { get; set; }

    [Column(TypeName = "TEXT")]
    public string? PatchHistoryJson { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }

    public ICollection<MetricSnapshot> MetricSnapshots { get; set; } = new List<MetricSnapshot>();
}

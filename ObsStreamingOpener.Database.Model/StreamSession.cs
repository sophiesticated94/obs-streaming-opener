using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class StreamSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; } = ProviderKind.YouTube;

    [Required]
    [MaxLength(256)]
    public string ExternalSessionId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = "Current stream";

    [MaxLength(256)]
    public string? ExternalStreamId { get; set; }

    [MaxLength(256)]
    public string? ExternalLiveChatId { get; set; }

    public Guid? ProviderResourceId { get; set; }

    public DateTimeOffset? ScheduledStartAt { get; set; }

    public DateTimeOffset? ActualStartAt { get; set; }

    public DateTimeOffset? ActualEndAt { get; set; }

    [MaxLength(128)]
    public string? Status { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "TEXT")]
    public string? PayloadSummaryJson { get; set; }

    public MonitoredChannel? MonitoredChannel { get; set; }

    [ForeignKey(nameof(ProviderResourceId))]
    public ProviderResource? ProviderResource { get; set; }

    public ICollection<StreamEvent> Events { get; set; } = new List<StreamEvent>();

    public ICollection<Tip> Tips { get; set; } = new List<Tip>();

    public ICollection<StreamAlert> Alerts { get; set; } = new List<StreamAlert>();

    public ICollection<MetricSnapshot> MetricSnapshots { get; set; } = new List<MetricSnapshot>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class StreamEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    public Guid? StreamSessionId { get; set; }

    public Guid? AudienceMemberId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    public StreamEventType EventType { get; set; }

    [MaxLength(256)]
    public string? ExternalEventId { get; set; }

    [MaxLength(256)]
    public string? ActorName { get; set; }

    [MaxLength(256)]
    public string? ActorExternalId { get; set; }

    [MaxLength(512)]
    public string? Title { get; set; }

    [MaxLength(2048)]
    public string? Message { get; set; }

    public decimal? Amount { get; set; }

    [MaxLength(8)]
    public string? Currency { get; set; }

    [Required]
    public DateTimeOffset OccurredAt { get; set; }

    [Required]
    public DateTimeOffset StoredAt { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "TEXT")]
    public string? RawPayloadJson { get; set; }

    [ForeignKey(nameof(StreamSessionId))]
    public StreamSession? StreamSession { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }

    [ForeignKey(nameof(AudienceMemberId))]
    public AudienceMember? AudienceMember { get; set; }
}

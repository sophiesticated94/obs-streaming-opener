using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class AudienceRelationshipPeriod
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    [Required]
    public Guid AudienceMemberId { get; set; }

    [Required]
    public AudienceRelationshipKind RelationshipKind { get; set; }

    [Required]
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public bool IsEstimated { get; set; }

    [MaxLength(256)]
    public string? SupportExternalId { get; set; }

    [MaxLength(256)]
    public string? TierName { get; set; }

    public RelationshipStatus Status { get; set; } = RelationshipStatus.Active;

    public BillingCadence BillingCadence { get; set; } = BillingCadence.Unknown;

    public decimal? Amount { get; set; }

    [MaxLength(32)]
    public string? Currency { get; set; }

    public DateTimeOffset? LastChargeAt { get; set; }

    public DateTimeOffset? NextChargeAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid? SourceEventId { get; set; }

    [Column(TypeName = "TEXT")]
    public string? RawPayloadJson { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }

    [ForeignKey(nameof(AudienceMemberId))]
    public AudienceMember? AudienceMember { get; set; }

    [ForeignKey(nameof(SourceEventId))]
    public StreamEvent? SourceEvent { get; set; }
}

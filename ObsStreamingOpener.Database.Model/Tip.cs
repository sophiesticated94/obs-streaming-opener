using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class Tip
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    public Guid? StreamSessionId { get; set; }

    [Required]
    public Guid StreamEventId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    public TipKind TipKind { get; set; } = TipKind.Donation;

    [Required]
    public TipStatus Status { get; set; } = TipStatus.Settled;

    [Required]
    public TipSource Source { get; set; } = TipSource.Provider;

    [Required]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Unknown;

    [MaxLength(256)]
    public string? ExternalTipId { get; set; }

    public bool IsSyntheticExternalId { get; set; }

    public Guid? RefundedTipId { get; set; }

    [MaxLength(256)]
    public string? ActorName { get; set; }

    [MaxLength(256)]
    public string? ActorExternalId { get; set; }

    [Required]
    public decimal Amount { get; set; }

    public decimal? GrossAmount { get; set; }

    public decimal? KnownNetAmount { get; set; }

    public decimal? EstimatedNetAmount { get; set; }

    public decimal? PlatformFee { get; set; }

    public decimal? ProcessorFee { get; set; }

    public decimal? PayoutFee { get; set; }

    [Required]
    [MaxLength(32)]
    public string Currency { get; set; } = string.Empty;

    [Column(TypeName = "TEXT")]
    public string? FeeLinesJson { get; set; }

    [MaxLength(256)]
    public string? CampaignExternalId { get; set; }

    [MaxLength(256)]
    public string? PayoutExternalId { get; set; }

    [MaxLength(256)]
    public string? SupportExternalId { get; set; }

    [MaxLength(256)]
    public string? PatronTierName { get; set; }

    [MaxLength(2048)]
    public string? Message { get; set; }

    [Required]
    public DateTimeOffset OccurredAt { get; set; }

    [Required]
    public DateTimeOffset StoredAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "TEXT")]
    public string? ContextJson { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }

    [ForeignKey(nameof(StreamSessionId))]
    public StreamSession? StreamSession { get; set; }

    [ForeignKey(nameof(StreamEventId))]
    public StreamEvent? StreamEvent { get; set; }

    [ForeignKey(nameof(RefundedTipId))]
    public Tip? RefundedTip { get; set; }

    public ICollection<Tip> Refunds { get; set; } = new List<Tip>();
}

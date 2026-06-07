using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class ProviderMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    public Guid? StreamSessionId { get; set; }

    public Guid? ProviderResourceId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    public MessageSource Source { get; set; }

    [MaxLength(256)]
    public string? ExternalMessageId { get; set; }

    [Required]
    [MaxLength(512)]
    public string IdentityKey { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? AuthorExternalId { get; set; }

    [MaxLength(256)]
    public string? AuthorDisplayName { get; set; }

    [MaxLength(1024)]
    public string? AuthorProfileImageUrl { get; set; }

    [MaxLength(4096)]
    public string? MessageText { get; set; }

    [Required]
    public DateTimeOffset PublishedAt { get; set; }

    public long? LikeCount { get; set; }

    public bool IsOwner { get; set; }

    public bool IsModerator { get; set; }

    public bool IsVerified { get; set; }

    public bool IsSponsor { get; set; }

    public decimal? Amount { get; set; }

    [MaxLength(8)]
    public string? Currency { get; set; }

    [Required]
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "TEXT")]
    public string? PayloadSummaryJson { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }

    [ForeignKey(nameof(StreamSessionId))]
    public StreamSession? StreamSession { get; set; }

    [ForeignKey(nameof(ProviderResourceId))]
    public ProviderResource? ProviderResource { get; set; }
}

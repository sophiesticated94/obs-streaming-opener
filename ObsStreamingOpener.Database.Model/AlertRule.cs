using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class AlertRule
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredChannelId { get; set; }

    [Required]
    public StreamEventType EventType { get; set; }

    public bool Enabled { get; set; } = true;

    public decimal? MinimumAmount { get; set; }

    public int DurationSeconds { get; set; } = 6;

    [MaxLength(64)]
    public string VisualStyle { get; set; } = "default";

    [MaxLength(256)]
    public string? TitleTemplate { get; set; }

    [MaxLength(2048)]
    public string? MessageTemplate { get; set; }

    [MaxLength(1024)]
    public string? MediaUrl { get; set; }

    [MaxLength(1024)]
    public string? SoundUrl { get; set; }

    [Required]
    public DateTimeOffset UpdatedAtUtc { get; set; }

    [ForeignKey(nameof(MonitoredChannelId))]
    public MonitoredChannel? MonitoredChannel { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class MonitoredChannel
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredAccountId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    [MaxLength(256)]
    public string ExternalChannelId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = "Default channel";

    [MaxLength(1024)]
    public string? Url { get; set; }

    public bool IsDefault { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [ForeignKey(nameof(MonitoredAccountId))]
    public MonitoredAccount? MonitoredAccount { get; set; }

    public ICollection<ProviderConnection> ProviderConnections { get; set; } = new List<ProviderConnection>();

    public ICollection<StreamSession> StreamSessions { get; set; } = new List<StreamSession>();

    public ICollection<StreamEvent> Events { get; set; } = new List<StreamEvent>();

    public ICollection<Tip> Tips { get; set; } = new List<Tip>();

    public ICollection<StreamAlert> Alerts { get; set; } = new List<StreamAlert>();

    public ICollection<AlertRule> AlertRules { get; set; } = new List<AlertRule>();

    public ICollection<ProviderMessage> Messages { get; set; } = new List<ProviderMessage>();

    public ICollection<MetricSnapshot> MetricSnapshots { get; set; } = new List<MetricSnapshot>();

    public ICollection<ProviderResource> ProviderResources { get; set; } = new List<ProviderResource>();

    public ICollection<AudienceRelationshipPeriod> AudienceRelationshipPeriods { get; set; } = new List<AudienceRelationshipPeriod>();
}

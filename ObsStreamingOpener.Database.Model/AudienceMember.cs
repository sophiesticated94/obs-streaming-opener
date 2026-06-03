using System.ComponentModel.DataAnnotations;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class AudienceMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    [MaxLength(256)]
    public string ExternalAudienceId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? DisplayName { get; set; }

    [MaxLength(1024)]
    public string? ProfileUrl { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AudienceRelationshipPeriod> RelationshipPeriods { get; set; } = new List<AudienceRelationshipPeriod>();
}

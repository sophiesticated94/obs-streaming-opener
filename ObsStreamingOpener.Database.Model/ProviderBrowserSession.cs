using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class ProviderBrowserSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public ProviderKind Provider { get; set; }

    [Column(TypeName = "TEXT")]
    public string? EncryptedStorageStateJson { get; set; }

    [Required]
    [MaxLength(64)]
    public string Status { get; set; } = "NeedsLogin";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastValidatedAt { get; set; }

    public DateTimeOffset? DisconnectedAt { get; set; }
}

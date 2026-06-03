using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ObsStreamingOpener.Database.Model;

public sealed class ProviderCursor
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProviderConnectionId { get; set; }

    [Required]
    [MaxLength(128)]
    public string CursorName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string? CursorValue { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "TEXT")]
    public string? MetadataJson { get; set; }

    [ForeignKey(nameof(ProviderConnectionId))]
    public ProviderConnection? ProviderConnection { get; set; }
}

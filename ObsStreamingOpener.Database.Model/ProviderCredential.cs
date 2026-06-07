using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database.Model;

public sealed class ProviderCredential
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MonitoredAccountId { get; set; }

    [Required]
    public ProviderKind Provider { get; set; }

    [Required]
    [MaxLength(256)]
    public string ExternalAccountId { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Email { get; set; }

    [MaxLength(512)]
    public string? DisplayName { get; set; }

    [Column(TypeName = "TEXT")]
    public string? EncryptedAccessToken { get; set; }

    [Column(TypeName = "TEXT")]
    public string? EncryptedRefreshToken { get; set; }

    [MaxLength(64)]
    public string? TokenType { get; set; }

    [Column(TypeName = "TEXT")]
    public string Scopes { get; set; } = string.Empty;

    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastRefreshedAt { get; set; }

    public DateTimeOffset? DisconnectedAt { get; set; }

    [ForeignKey(nameof(MonitoredAccountId))]
    public MonitoredAccount? MonitoredAccount { get; set; }
}

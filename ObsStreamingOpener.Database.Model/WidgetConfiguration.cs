using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ObsStreamingOpener.Database.Model;

public sealed class WidgetConfiguration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(128)]
    public string WidgetKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string WidgetType { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Theme { get; set; } = "default";

    [Column(TypeName = "TEXT")]
    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

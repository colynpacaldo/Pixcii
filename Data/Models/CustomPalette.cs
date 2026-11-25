using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Pixcii.Data.Models;

public class CustomPalette
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of hex color strings (e.g. ["#0F380F","#306230"]).
    /// </summary>
    [Required]
    public string HexColorsJson { get; set; } = "[]";

    [Required]
    [MaxLength(80)]
    public string CreatedBy { get; set; } = "System";

    [NotMapped]
    public string[] HexColors
    {
        get => JsonSerializer.Deserialize<string[]>(HexColorsJson) ?? [];
        set => HexColorsJson = JsonSerializer.Serialize(value);
    }
}

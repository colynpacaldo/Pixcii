using System.ComponentModel.DataAnnotations;

namespace Pixcii.Data.Models;

public class ArtFrame
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StudioProjectId { get; set; }

    [Range(0, int.MaxValue)]
    public int OrderIndex { get; set; }

    /// <summary>
    /// Stores raw ASCII text or JSON color-grid payload for the frame.
    /// </summary>
    public string? AsciiContent { get; set; }

    /// <summary>
    /// Stores Base64-encoded pixel grid or raster frame data.
    /// </summary>
    public string? PixelDataBase64 { get; set; }

    [Range(10, 60000)]
    public int DurationMs { get; set; } = 120;

    public StudioProject? StudioProject { get; set; }
}

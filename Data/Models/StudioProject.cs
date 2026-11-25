using System.ComponentModel.DataAnnotations;

namespace Pixcii.Data.Models;

public enum ProjectMode
{
    Ascii = 0,
    PixelArt = 1
}

public class StudioProject
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = "Untitled Project";

    [Required]
    [MaxLength(80)]
    public string AuthorName { get; set; } = "Anonymous";

    [Required]
    public ProjectMode Mode { get; set; } = ProjectMode.PixelArt;

    [Range(1, 512)]
    public int Width { get; set; } = 32;

    [Range(1, 512)]
    public int Height { get; set; } = 32;

    [Range(1, 60)]
    public int Fps { get; set; } = 8;

    [Required]
    [MaxLength(80)]
    public string PaletteName { get; set; } = "PICO-8";

    [Required]
    [MaxLength(128)]
    public string CharacterRamp { get; set; } = " .:-=+*#%@";

    public bool IsPublic { get; set; }

    public Guid? RemixedFromId { get; set; }

    public StudioProject? RemixedFrom { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ArtFrame> ArtFrames { get; set; } = [];

    public ICollection<ProjectReaction> Reactions { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace Pixcii.Data.Models;

public class ProjectReaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid StudioProjectId { get; set; }

    [Required]
    [MaxLength(32)]
    public string ReactionType { get; set; } = "Art";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StudioProject? StudioProject { get; set; }
}

using Microsoft.EntityFrameworkCore;
using Pixcii.Data;
using Pixcii.Data.Models;

namespace Pixcii.Services;

public sealed class ProjectService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<StudioProject> CreateProjectAsync(
        string title,
        string authorName,
        ProjectMode mode = ProjectMode.PixelArt,
        int width = 32,
        int height = 32,
        int fps = 8,
        string paletteName = "PICO-8",
        string characterRamp = " .:-=+*#%@",
        bool isPublic = false,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;
        var clampedFps = Math.Clamp(fps, 1, 60);

        var project = new StudioProject
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(title) ? "Untitled Project" : title.Trim(),
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Anonymous" : authorName.Trim(),
            Mode = mode,
            Width = width,
            Height = height,
            Fps = clampedFps,
            PaletteName = paletteName,
            CharacterRamp = characterRamp,
            IsPublic = isPublic,
            CreatedAt = now,
            UpdatedAt = now,
            ArtFrames =
            [
                new ArtFrame
                {
                    Id = Guid.NewGuid(),
                    OrderIndex = 0,
                    DurationMs = Math.Max(10, 1000 / clampedFps)
                }
            ]
        };

        db.StudioProjects.Add(project);
        await db.SaveChangesAsync(ct);
        return project;
    }

    public async Task<StudioProject?> GetProjectWithFramesAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        return await db.StudioProjects
            .AsNoTracking()
            .Include(p => p.ArtFrames.OrderBy(f => f.OrderIndex))
            .Include(p => p.Reactions)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);
    }

    public async Task SaveFramesAsync(
        Guid projectId,
        IEnumerable<ArtFrame> frames,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var project = await db.StudioProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new KeyNotFoundException($"StudioProject '{projectId}' was not found.");

        await db.ArtFrames
            .Where(f => f.StudioProjectId == projectId)
            .ExecuteDeleteAsync(ct);

        var normalizedFrames = frames
            .Select((frame, index) => new ArtFrame
            {
                Id = frame.Id == Guid.Empty ? Guid.NewGuid() : frame.Id,
                StudioProjectId = projectId,
                OrderIndex = index,
                AsciiContent = frame.AsciiContent,
                PixelDataBase64 = frame.PixelDataBase64,
                DurationMs = frame.DurationMs > 0 ? frame.DurationMs : 120
            })
            .ToList();

        if (normalizedFrames.Count > 0)
        {
            db.ArtFrames.AddRange(normalizedFrames);
        }

        project.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<StudioProject>> GetPublicGalleryAsync(
        int page = 1,
        int pageSize = 24,
        ProjectMode? mode = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.StudioProjects
            .AsNoTracking()
            .Where(p => p.IsPublic);

        if (mode.HasValue)
        {
            query = query.Where(p => p.Mode == mode.Value);
        }

        return await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Include(p => p.ArtFrames.OrderBy(f => f.OrderIndex).Take(1))
            .Include(p => p.Reactions)
            .ToListAsync(ct);
    }

    public async Task<StudioProject> ForkProjectAsync(
        Guid sourceProjectId,
        string newAuthorName,
        string? newTitle = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var source = await db.StudioProjects
            .AsNoTracking()
            .Include(p => p.ArtFrames.OrderBy(f => f.OrderIndex))
            .FirstOrDefaultAsync(p => p.Id == sourceProjectId, ct)
            ?? throw new KeyNotFoundException($"Source project '{sourceProjectId}' was not found.");

        var now = DateTime.UtcNow;
        var forkedProjectId = Guid.NewGuid();

        var fork = new StudioProject
        {
            Id = forkedProjectId,
            Title = string.IsNullOrWhiteSpace(newTitle) ? $"{source.Title} (Remix)" : newTitle.Trim(),
            AuthorName = string.IsNullOrWhiteSpace(newAuthorName) ? "Anonymous" : newAuthorName.Trim(),
            Mode = source.Mode,
            Width = source.Width,
            Height = source.Height,
            Fps = source.Fps,
            PaletteName = source.PaletteName,
            CharacterRamp = source.CharacterRamp,
            IsPublic = false,
            RemixedFromId = source.Id,
            CreatedAt = now,
            UpdatedAt = now,
            ArtFrames = source.ArtFrames
                .OrderBy(f => f.OrderIndex)
                .Select((f, idx) => new ArtFrame
                {
                    Id = Guid.NewGuid(),
                    StudioProjectId = forkedProjectId,
                    OrderIndex = idx,
                    AsciiContent = f.AsciiContent,
                    PixelDataBase64 = f.PixelDataBase64,
                    DurationMs = f.DurationMs
                })
                .ToList()
        };

        db.StudioProjects.Add(fork);
        await db.SaveChangesAsync(ct);
        return fork;
    }
}

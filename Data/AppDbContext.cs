using Microsoft.EntityFrameworkCore;
using Pixcii.Data.Models;

namespace Pixcii.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StudioProject> StudioProjects => Set<StudioProject>();
    public DbSet<ArtFrame> ArtFrames => Set<ArtFrame>();
    public DbSet<CustomPalette> CustomPalettes => Set<CustomPalette>();
    public DbSet<ProjectReaction> ProjectReactions => Set<ProjectReaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StudioProject>(entity =>
        {
            entity.Property(p => p.Mode)
                .HasConversion<string>()
                .HasMaxLength(24);

            entity.Property(p => p.Fps)
                .HasDefaultValue(8);

            entity.HasIndex(p => new { p.IsPublic, p.UpdatedAt });

            // Cascade delete all ArtFrames when a StudioProject is deleted.
            entity.HasMany(p => p.ArtFrames)
                .WithOne(f => f.StudioProject)
                .HasForeignKey(f => f.StudioProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade delete reactions when a StudioProject is deleted.
            entity.HasMany(p => p.Reactions)
                .WithOne(r => r.StudioProject)
                .HasForeignKey(r => r.StudioProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Preserve remixes if the original source project is deleted.
            entity.HasOne(p => p.RemixedFrom)
                .WithMany()
                .HasForeignKey(p => p.RemixedFromId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ArtFrame>(entity =>
        {
            entity.Property(f => f.DurationMs)
                .HasDefaultValue(120);

            entity.HasIndex(f => new { f.StudioProjectId, f.OrderIndex });
        });

        modelBuilder.Entity<CustomPalette>(entity =>
        {
            entity.HasIndex(p => p.Name).IsUnique();

            entity.HasData(
                new CustomPalette
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                    Name = "GameBoy 4-Color",
                    HexColorsJson = """["#0F380F","#306230","#8BAC0F","#9BBC0F"]""",
                    CreatedBy = "Pixcii Built-in"
                },
                new CustomPalette
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                    Name = "PICO-8",
                    HexColorsJson = """["#000000","#1D2B53","#7E2553","#008751","#AB5236","#5F574F","#C2C3C7","#FFF1E8","#FF004D","#FFA300","#FFEC27","#00E436","#29ADFF","#83769C","#FF77A8","#FFCCAA"]""",
                    CreatedBy = "Pixcii Built-in"
                },
                new CustomPalette
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                    Name = "NES Retro",
                    HexColorsJson = """["#000000","#FCFCFC","#BCBCBC","#7C7C7C","#A40000","#F83800","#AC7C00","#F8B800","#005800","#00A800","#0078F8","#3CBCFC","#0000BC","#6844FC","#D800CC","#F878F8"]""",
                    CreatedBy = "Pixcii Built-in"
                },
                new CustomPalette
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                    Name = "Matrix Terminal",
                    HexColorsJson = """["#020B04","#06290F","#0D5E22","#16A34A","#22C55E","#4ADE80","#86EFAC","#DCFCE7"]""",
                    CreatedBy = "Pixcii Built-in"
                }
            );
        });

        modelBuilder.Entity<ProjectReaction>(entity =>
        {
            entity.HasIndex(r => new { r.StudioProjectId, r.ReactionType });
        });
    }
}

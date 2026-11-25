using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Pixcii.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomPalettes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    HexColorsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomPalettes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudioProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Fps = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 8),
                    PaletteName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CharacterRamp = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemixedFromId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudioProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudioProjects_StudioProjects_RemixedFromId",
                        column: x => x.RemixedFromId,
                        principalTable: "StudioProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ArtFrames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StudioProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    AsciiContent = table.Column<string>(type: "TEXT", nullable: true),
                    PixelDataBase64 = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 120)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtFrames_StudioProjects_StudioProjectId",
                        column: x => x.StudioProjectId,
                        principalTable: "StudioProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectReactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StudioProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReactionType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectReactions_StudioProjects_StudioProjectId",
                        column: x => x.StudioProjectId,
                        principalTable: "StudioProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CustomPalettes",
                columns: new[] { "Id", "CreatedBy", "HexColorsJson", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), "Pixcii Built-in", "[\"#0F380F\",\"#306230\",\"#8BAC0F\",\"#9BBC0F\"]", "GameBoy 4-Color" },
                    { new Guid("11111111-1111-1111-1111-111111111102"), "Pixcii Built-in", "[\"#000000\",\"#1D2B53\",\"#7E2553\",\"#008751\",\"#AB5236\",\"#5F574F\",\"#C2C3C7\",\"#FFF1E8\",\"#FF004D\",\"#FFA300\",\"#FFEC27\",\"#00E436\",\"#29ADFF\",\"#83769C\",\"#FF77A8\",\"#FFCCAA\"]", "PICO-8" },
                    { new Guid("11111111-1111-1111-1111-111111111103"), "Pixcii Built-in", "[\"#000000\",\"#FCFCFC\",\"#BCBCBC\",\"#7C7C7C\",\"#A40000\",\"#F83800\",\"#AC7C00\",\"#F8B800\",\"#005800\",\"#00A800\",\"#0078F8\",\"#3CBCFC\",\"#0000BC\",\"#6844FC\",\"#D800CC\",\"#F878F8\"]", "NES Retro" },
                    { new Guid("11111111-1111-1111-1111-111111111104"), "Pixcii Built-in", "[\"#020B04\",\"#06290F\",\"#0D5E22\",\"#16A34A\",\"#22C55E\",\"#4ADE80\",\"#86EFAC\",\"#DCFCE7\"]", "Matrix Terminal" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtFrames_StudioProjectId_OrderIndex",
                table: "ArtFrames",
                columns: new[] { "StudioProjectId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomPalettes_Name",
                table: "CustomPalettes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectReactions_StudioProjectId_ReactionType",
                table: "ProjectReactions",
                columns: new[] { "StudioProjectId", "ReactionType" });

            migrationBuilder.CreateIndex(
                name: "IX_StudioProjects_IsPublic_UpdatedAt",
                table: "StudioProjects",
                columns: new[] { "IsPublic", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudioProjects_RemixedFromId",
                table: "StudioProjects",
                column: "RemixedFromId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtFrames");

            migrationBuilder.DropTable(
                name: "CustomPalettes");

            migrationBuilder.DropTable(
                name: "ProjectReactions");

            migrationBuilder.DropTable(
                name: "StudioProjects");
        }
    }
}

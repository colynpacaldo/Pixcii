using System.Text.Json;
using Pixcii.Data.Models;

namespace Pixcii.Data;

public enum AnimationFxPreset
{
    GlitchVhs = 0,
    WaveWobble = 1,
    PulseBreathing = 2,
    Bounce = 3
}

/// <summary>
/// Serializable frame payload for ASCII frames stored in ArtFrame.AsciiContent.
/// </summary>
public sealed record AsciiFramePayload(
    int Columns,
    int Rows,
    AsciiColorMode ColorMode,
    string[] Glyphs,
    string[] Colors
);

/// <summary>
/// Serializable grid payload for Pixel Art frames stored in ArtFrame.AsciiContent.
/// </summary>
public sealed record PixelFramePayload(
    int Width,
    int Height,
    string[] FlatHexColors
);

public static class FrameSerialization
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static string SerializeAscii(AsciiResult result)
    {
        var total = result.Rows * result.Columns;
        var glyphs = new string[total];
        var colors = new string[total];

        var idx = 0;
        for (var r = 0; r < result.Rows; r++)
        {
            for (var c = 0; c < result.Columns; c++)
            {
                var cell = result.Grid[r, c];
                glyphs[idx] = cell.Glyph.ToString();
                colors[idx] = cell.HexColor;
                idx++;
            }
        }

        var payload = new AsciiFramePayload(
            result.Columns,
            result.Rows,
            AsciiColorMode.FullColorHtml,
            glyphs,
            colors);

        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    public static AsciiResult? DeserializeAscii(
        string? content,
        Func<AsciiCell[,], AsciiColorMode, (string PlainText, string ColoredHtml)> htmlBuilder,
        AsciiColorMode fallbackMode = AsciiColorMode.FullColorHtml)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<AsciiFramePayload>(content, JsonOpts);
            if (payload is not null && payload.Columns > 0 && payload.Rows > 0)
            {
                var grid = new AsciiCell[payload.Rows, payload.Columns];
                var idx = 0;
                for (var r = 0; r < payload.Rows; r++)
                {
                    for (var c = 0; c < payload.Columns; c++)
                    {
                        var glyph = idx < payload.Glyphs.Length && payload.Glyphs[idx].Length > 0
                            ? payload.Glyphs[idx][0]
                            : ' ';
                        var color = idx < payload.Colors.Length && !string.IsNullOrWhiteSpace(payload.Colors[idx])
                            ? payload.Colors[idx]
                            : "#E2E8F0";
                        grid[r, c] = new AsciiCell(glyph, color);
                        idx++;
                    }
                }

                var (plainText, coloredHtml) = htmlBuilder(grid, fallbackMode);
                return new AsciiResult(payload.Columns, payload.Rows, plainText, coloredHtml, grid);
            }
        }
        catch (JsonException)
        {
            // Fallback: treat AsciiContent as raw newline-delimited plain text.
        }

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var rows = lines.Length;
        var cols = lines.Max(l => l.Length);
        if (rows == 0 || cols == 0)
        {
            return null;
        }

        var textGrid = new AsciiCell[rows, cols];
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var ch = c < lines[r].Length ? lines[r][c] : ' ';
                textGrid[r, c] = new AsciiCell(ch, "#E2E8F0");
            }
        }

        var built = htmlBuilder(textGrid, fallbackMode);
        return new AsciiResult(cols, rows, built.PlainText, built.ColoredHtml, textGrid);
    }

    public static string SerializePixelGrid(PixelArtResult result)
    {
        var flat = new string[result.Height * result.Width];
        var idx = 0;
        for (var r = 0; r < result.Height; r++)
        {
            for (var c = 0; c < result.Width; c++)
            {
                flat[idx++] = result.HexGrid[r, c];
            }
        }

        return JsonSerializer.Serialize(new PixelFramePayload(result.Width, result.Height, flat), JsonOpts);
    }

    public static string[,]? DeserializePixelGrid(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<PixelFramePayload>(json, JsonOpts);
            if (payload is null || payload.Width <= 0 || payload.Height <= 0)
            {
                return null;
            }

            var grid = new string[payload.Height, payload.Width];
            var idx = 0;
            for (var r = 0; r < payload.Height; r++)
            {
                for (var c = 0; c < payload.Width; c++)
                {
                    grid[r, c] = idx < payload.FlatHexColors.Length ? payload.FlatHexColors[idx] : string.Empty;
                    idx++;
                }
            }

            return grid;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ArtFrame ToArtFrame(AsciiResult ascii, int orderIndex, int durationMs) => new()
    {
        Id = Guid.NewGuid(),
        OrderIndex = orderIndex,
        AsciiContent = SerializeAscii(ascii),
        PixelDataBase64 = null,
        DurationMs = durationMs
    };

    public static ArtFrame ToArtFrame(PixelArtResult pixel, int orderIndex, int durationMs) => new()
    {
        Id = Guid.NewGuid(),
        OrderIndex = orderIndex,
        AsciiContent = SerializePixelGrid(pixel),
        PixelDataBase64 = pixel.Base64Png,
        DurationMs = durationMs
    };
}

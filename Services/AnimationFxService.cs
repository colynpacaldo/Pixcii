using Pixcii.Data;
using Pixcii.Data.Models;
using SixLabors.ImageSharp.PixelFormats;

namespace Pixcii.Services;

/// <summary>
/// Generates 6-to-8 frame seamless procedural animation loops from a single static
/// ASCII or Pixel Art frame for instant live studio demos.
/// </summary>
public sealed class AnimationFxService
{
    private const int DefaultLoopFrameCount = 8;

    private static readonly char[] GlitchChars = ['@', '#', '$', '%', '&', '?', '/', '\\', '~', '^', '*'];
    private static readonly string[] GlitchColors = ["#22D3EE", "#F43F5E", "#FACC15", "#F8FAFC", "#A78BFA"];
    private static readonly int[] BounceRowOffsets = [0, -1, -2, -2, -1, 0, 1, 0];

    public IReadOnlyList<ArtFrame> GenerateAsciiLoop(
        AsciiResult source,
        AnimationFxPreset preset,
        AsciiColorMode colorMode = AsciiColorMode.FullColorHtml,
        int fps = 8,
        int frameCount = DefaultLoopFrameCount)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = Math.Clamp(frameCount, 6, 8);
        var durationMs = Math.Max(40, 1000 / Math.Clamp(fps, 1, 24));
        var frames = new List<ArtFrame>(count);

        for (var f = 0; f < count; f++)
        {
            var mutatedGrid = preset switch
            {
                AnimationFxPreset.GlitchVhs => ApplyAsciiGlitch(source.Grid, f, count),
                AnimationFxPreset.WaveWobble => ApplyAsciiWave(source.Grid, f, count),
                AnimationFxPreset.PulseBreathing => ApplyAsciiPulse(source.Grid, f, count),
                AnimationFxPreset.Bounce => ApplyAsciiBounce(source.Grid, f),
                _ => CloneAsciiGrid(source.Grid)
            };

            var (plain, html) = MediaConversionService.BuildAsciiTextAndHtml(mutatedGrid, colorMode);
            var frameResult = new AsciiResult(source.Columns, source.Rows, plain, html, mutatedGrid);
            frames.Add(FrameSerialization.ToArtFrame(frameResult, f, durationMs));
        }

        return frames;
    }

    public IReadOnlyList<ArtFrame> GeneratePixelArtLoop(
        PixelArtResult source,
        AnimationFxPreset preset,
        IReadOnlyList<string>? hexPalette = null,
        int fps = 8,
        int frameCount = DefaultLoopFrameCount)
    {
        ArgumentNullException.ThrowIfNull(source);

        var count = Math.Clamp(frameCount, 6, 8);
        var durationMs = Math.Max(40, 1000 / Math.Clamp(fps, 1, 24));
        var frames = new List<ArtFrame>(count);

        var parsedPalette = ParseOptionalPalette(hexPalette);

        for (var f = 0; f < count; f++)
        {
            var mutatedGrid = preset switch
            {
                AnimationFxPreset.GlitchVhs => ApplyPixelGlitch(source.HexGrid, f, count),
                AnimationFxPreset.WaveWobble => ApplyPixelWave(source.HexGrid, f, count),
                AnimationFxPreset.PulseBreathing => ApplyPixelPulse(source.HexGrid, f, count, parsedPalette),
                AnimationFxPreset.Bounce => ApplyPixelBounce(source.HexGrid, f),
                _ => CloneHexGrid(source.HexGrid)
            };

            var rendered = MediaConversionService.RenderHexGridToPixelArt(mutatedGrid);
            frames.Add(FrameSerialization.ToArtFrame(rendered, f, durationMs));
        }

        return frames;
    }

    // -------------------------------------------------------------------------
    // ASCII Procedural FX
    // -------------------------------------------------------------------------

    private static AsciiCell[,] ApplyAsciiGlitch(AsciiCell[,] source, int frameIndex, int frameCount)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = CloneAsciiGrid(source);

        // Keep frame 0 and last frame close to clean for seamless looping.
        if (frameIndex == 0)
        {
            return result;
        }

        var rng = new Random(1337 + (frameIndex * 97));
        var sliceCount = frameIndex == frameCount - 1 ? 1 : rng.Next(2, 5);

        for (var s = 0; s < sliceCount; s++)
        {
            var startRow = rng.Next(0, rows);
            var height = rng.Next(1, Math.Max(2, rows / 6));
            var shift = rng.Next(-4, 5);
            if (shift == 0) shift = 2;

            for (var r = startRow; r < Math.Min(rows, startRow + height); r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var srcCol = ((c - shift) % cols + cols) % cols;
                    var cell = source[r, srcCol];

                    if (rng.NextDouble() < 0.14 && cell.Glyph != ' ')
                    {
                        var glyph = GlitchChars[rng.Next(GlitchChars.Length)];
                        var color = GlitchColors[rng.Next(GlitchColors.Length)];
                        result[r, c] = new AsciiCell(glyph, color);
                    }
                    else if (rng.NextDouble() < 0.25 && cell.Glyph != ' ')
                    {
                        result[r, c] = cell with { HexColor = s % 2 == 0 ? "#22D3EE" : "#F43F5E" };
                    }
                    else
                    {
                        result[r, c] = cell;
                    }
                }
            }
        }

        return result;
    }

    private static AsciiCell[,] ApplyAsciiWave(AsciiCell[,] source, int frameIndex, int frameCount)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = new AsciiCell[rows, cols];

        var phase = (2.0 * Math.PI * frameIndex) / frameCount;
        var amplitude = Math.Max(1.0, Math.Min(3.0, cols * 0.04));

        for (var r = 0; r < rows; r++)
        {
            var offset = (int)Math.Round(amplitude * Math.Sin(phase + (r * 0.55)));
            for (var c = 0; c < cols; c++)
            {
                var srcCol = c - offset;
                result[r, c] = srcCol >= 0 && srcCol < cols
                    ? source[r, srcCol]
                    : new AsciiCell(' ', "#E2E8F0");
            }
        }

        return result;
    }

    private static AsciiCell[,] ApplyAsciiPulse(AsciiCell[,] source, int frameIndex, int frameCount)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = new AsciiCell[rows, cols];

        // Smooth 0 -> 1 -> 0 cosine breathing curve.
        var pulse = 0.5 - (0.5 * Math.Cos((2.0 * Math.PI * frameIndex) / frameCount));
        var brightnessFactor = 0.65 + (pulse * 0.70);

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var cell = source[r, c];
                if (cell.Glyph == ' ')
                {
                    result[r, c] = cell;
                    continue;
                }

                var rgb = MediaConversionService.ParseHexColor(cell.HexColor);
                var scaled = ScaleColor(rgb, brightnessFactor);
                result[r, c] = cell with { HexColor = MediaConversionService.FormatHexColor(scaled) };
            }
        }

        return result;
    }

    private static AsciiCell[,] ApplyAsciiBounce(AsciiCell[,] source, int frameIndex)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = new AsciiCell[rows, cols];
        var rowOffset = BounceRowOffsets[frameIndex % BounceRowOffsets.Length];

        for (var r = 0; r < rows; r++)
        {
            var srcRow = r - rowOffset;
            for (var c = 0; c < cols; c++)
            {
                result[r, c] = srcRow >= 0 && srcRow < rows
                    ? source[srcRow, c]
                    : new AsciiCell(' ', "#E2E8F0");
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Pixel Art Procedural FX
    // -------------------------------------------------------------------------

    private static string[,] ApplyPixelGlitch(string[,] source, int frameIndex, int frameCount)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = CloneHexGrid(source);

        if (frameIndex == 0)
        {
            return result;
        }

        var rng = new Random(4242 + (frameIndex * 131));
        var sliceCount = frameIndex == frameCount - 1 ? 1 : rng.Next(2, 5);

        for (var s = 0; s < sliceCount; s++)
        {
            var startRow = rng.Next(0, rows);
            var height = rng.Next(1, Math.Max(2, rows / 6));
            var shift = rng.Next(-4, 5);
            if (shift == 0) shift = -2;

            for (var r = startRow; r < Math.Min(rows, startRow + height); r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var srcCol = ((c - shift) % cols + cols) % cols;
                    var hex = source[r, srcCol];

                    if (!string.IsNullOrEmpty(hex) && rng.NextDouble() < 0.16)
                    {
                        result[r, c] = GlitchColors[rng.Next(GlitchColors.Length)];
                    }
                    else
                    {
                        result[r, c] = hex;
                    }
                }
            }
        }

        return result;
    }

    private static string[,] ApplyPixelWave(string[,] source, int frameIndex, int frameCount)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = new string[rows, cols];

        var phase = (2.0 * Math.PI * frameIndex) / frameCount;
        var amplitude = Math.Max(1.0, Math.Min(3.0, cols * 0.06));

        for (var r = 0; r < rows; r++)
        {
            var offset = (int)Math.Round(amplitude * Math.Sin(phase + (r * 0.45)));
            for (var c = 0; c < cols; c++)
            {
                var srcCol = c - offset;
                result[r, c] = srcCol >= 0 && srcCol < cols
                    ? source[r, srcCol]
                    : string.Empty;
            }
        }

        return result;
    }

    private static string[,] ApplyPixelPulse(
        string[,] source,
        int frameIndex,
        int frameCount,
        Rgba32[]? palette)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = new string[rows, cols];

        var pulse = 0.5 - (0.5 * Math.Cos((2.0 * Math.PI * frameIndex) / frameCount));
        var brightnessFactor = 0.65 + (pulse * 0.70);

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var hex = source[r, c];
                if (string.IsNullOrEmpty(hex))
                {
                    result[r, c] = string.Empty;
                    continue;
                }

                var rgb = MediaConversionService.ParseHexColor(hex);
                var scaled = ScaleColor(rgb, brightnessFactor);

                if (palette is { Length: > 0 })
                {
                    var quantized = MediaConversionService.FindNearestPaletteColor(
                        scaled.R, scaled.G, scaled.B, palette);
                    result[r, c] = MediaConversionService.FormatHexColor(quantized);
                }
                else
                {
                    result[r, c] = MediaConversionService.FormatHexColor(scaled);
                }
            }
        }

        return result;
    }

    private static string[,] ApplyPixelBounce(string[,] source, int frameIndex)
    {
        var rows = source.GetLength(0);
        var cols = source.GetLength(1);
        var result = new string[rows, cols];
        var rowOffset = BounceRowOffsets[frameIndex % BounceRowOffsets.Length];

        for (var r = 0; r < rows; r++)
        {
            var srcRow = r - rowOffset;
            for (var c = 0; c < cols; c++)
            {
                result[r, c] = srcRow >= 0 && srcRow < rows
                    ? source[srcRow, c]
                    : string.Empty;
            }
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Rgba32 ScaleColor(Rgba32 color, double factor) => new(
        (byte)Math.Clamp((int)Math.Round(color.R * factor), 0, 255),
        (byte)Math.Clamp((int)Math.Round(color.G * factor), 0, 255),
        (byte)Math.Clamp((int)Math.Round(color.B * factor), 0, 255),
        color.A);

    private static Rgba32[]? ParseOptionalPalette(IReadOnlyList<string>? hexPalette)
    {
        if (hexPalette is null || hexPalette.Count == 0)
        {
            return null;
        }

        var parsed = new Rgba32[hexPalette.Count];
        for (var i = 0; i < hexPalette.Count; i++)
        {
            parsed[i] = MediaConversionService.ParseHexColor(hexPalette[i]);
        }
        return parsed;
    }

    private static AsciiCell[,] CloneAsciiGrid(AsciiCell[,] source)
        => (AsciiCell[,])source.Clone();

    private static string[,] CloneHexGrid(string[,] source)
        => (string[,])source.Clone();
}

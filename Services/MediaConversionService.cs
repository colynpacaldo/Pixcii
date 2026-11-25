using System.Globalization;
using System.Net;
using System.Text;
using Pixcii.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace Pixcii.Services;

/// <summary>
/// Pure C# image-processing engine backed by SixLabors.ImageSharp.
/// Converts uploaded raster images (PNG/JPG/WebP) into colored ASCII art
/// and palette-quantized pixel art with zero UI framework dependencies.
/// </summary>
public sealed class MediaConversionService
{
    private const double DefaultFontAspectRatio = 0.5;
    private const int DefaultMinPreviewDimension = 256;

    // -------------------------------------------------------------------------
    // 1. Image-to-ASCII Pipeline
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts raw image bytes (PNG/JPG/WebP) into monochrome, Matrix-green, or full-color ASCII art.
    /// </summary>
    public Task<AsciiResult> ConvertToAsciiAsync(
        byte[] imageBytes,
        int targetWidth = 80,
        string characterRamp = AsciiRamps.Standard,
        bool invertBrightness = false,
        AsciiColorMode colorMode = AsciiColorMode.FullColorHtml,
        double fontAspectRatio = DefaultFontAspectRatio,
        float brightness = 1.0f,
        float contrast = 1.0f,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image byte buffer cannot be empty.", nameof(imageBytes));
        }

        using var stream = new MemoryStream(imageBytes, writable: false);
        return ConvertToAsciiAsync(
            stream,
            targetWidth,
            characterRamp,
            invertBrightness,
            colorMode,
            fontAspectRatio,
            brightness,
            contrast,
            ct);
    }

    /// <summary>
    /// Converts an uploaded image stream (PNG/JPG/WebP) into monochrome, Matrix-green, or full-color ASCII art.
    /// </summary>
    public async Task<AsciiResult> ConvertToAsciiAsync(
        Stream imageStream,
        int targetWidth = 80,
        string characterRamp = AsciiRamps.Standard,
        bool invertBrightness = false,
        AsciiColorMode colorMode = AsciiColorMode.FullColorHtml,
        double fontAspectRatio = DefaultFontAspectRatio,
        float brightness = 1.0f,
        float contrast = 1.0f,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (string.IsNullOrEmpty(characterRamp))
        {
            throw new ArgumentException("Character ramp must contain at least one character.", nameof(characterRamp));
        }

        var clampedWidth = Math.Clamp(targetWidth, 1, 512);
        var clampedBrightness = Math.Clamp(brightness, 0.1f, 3.0f);
        var clampedContrast = Math.Clamp(contrast, 0.1f, 3.0f);

        using var image = await Image.LoadAsync<Rgba32>(imageStream, ct);
        var targetHeight = CalculateAspectAdjustedHeight(image.Width, image.Height, clampedWidth, fontAspectRatio);

        // Downsample using Box resampling and apply tonal adjustments before luminance sampling.
        image.Mutate(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(clampedWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.Box
            });

            if (Math.Abs(clampedBrightness - 1.0f) > 0.001f)
            {
                ctx.Brightness(clampedBrightness);
            }

            if (Math.Abs(clampedContrast - 1.0f) > 0.001f)
            {
                ctx.Contrast(clampedContrast);
            }
        });

        var grid = new AsciiCell[targetHeight, clampedWidth];
        var plainBuilder = new StringBuilder(targetHeight * (clampedWidth + 1));
        var htmlBuilder = new StringBuilder(targetHeight * clampedWidth * 12);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                string? activeRunColor = null;
                var runText = new StringBuilder(clampedWidth);

                for (var x = 0; x < accessor.Width; x++)
                {
                    var pixel = row[x];
                    var (glyph, hexColor) = ConvertPixelToAsciiCell(
                        pixel,
                        characterRamp,
                        invertBrightness,
                        colorMode);

                    grid[y, x] = new AsciiCell(glyph, hexColor);
                    plainBuilder.Append(glyph);

                    if (colorMode == AsciiColorMode.Monochrome)
                    {
                        runText.Append(glyph);
                        continue;
                    }

                    // Batch consecutive characters sharing the same hex color into a single <span>.
                    if (activeRunColor is null)
                    {
                        activeRunColor = hexColor;
                        runText.Append(glyph);
                    }
                    else if (string.Equals(activeRunColor, hexColor, StringComparison.Ordinal))
                    {
                        runText.Append(glyph);
                    }
                    else
                    {
                        AppendColoredSpan(htmlBuilder, activeRunColor, runText);
                        runText.Clear();
                        activeRunColor = hexColor;
                        runText.Append(glyph);
                    }
                }

                if (colorMode == AsciiColorMode.Monochrome)
                {
                    htmlBuilder.Append(WebUtility.HtmlEncode(runText.ToString()));
                }
                else if (runText.Length > 0 && activeRunColor is not null)
                {
                    AppendColoredSpan(htmlBuilder, activeRunColor, runText);
                }

                if (y < accessor.Height - 1)
                {
                    plainBuilder.Append('\n');
                    htmlBuilder.Append('\n');
                }
            }
        });

        return new AsciiResult(
            Columns: clampedWidth,
            Rows: targetHeight,
            PlainText: plainBuilder.ToString(),
            ColoredHtml: htmlBuilder.ToString(),
            Grid: grid);
    }

    // -------------------------------------------------------------------------
    // 2. Image-to-Pixel Art Pipeline
    // -------------------------------------------------------------------------

    /// <summary>
    /// Downsamples raw image bytes to a target pixel grid, quantizes colors to a hex palette
    /// (with optional Floyd-Steinberg dithering), and produces a crisp NearestNeighbor-upscaled Base64 PNG.
    /// </summary>
    public Task<PixelArtResult> ConvertToPixelArtAsync(
        byte[] imageBytes,
        int gridSize = 32,
        IReadOnlyList<string>? hexPalette = null,
        bool applyDithering = false,
        PixelResampleMode resampleMode = PixelResampleMode.Box,
        float brightness = 1.0f,
        float contrast = 1.0f,
        bool invertBrightness = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image byte buffer cannot be empty.", nameof(imageBytes));
        }

        using var stream = new MemoryStream(imageBytes, writable: false);
        return ConvertToPixelArtAsync(
            stream,
            gridSize,
            hexPalette,
            applyDithering,
            resampleMode,
            brightness,
            contrast,
            invertBrightness,
            ct);
    }

    /// <summary>
    /// Downsamples an image stream to a target pixel grid, quantizes colors to a hex palette
    /// (with optional Floyd-Steinberg dithering), and produces a crisp NearestNeighbor-upscaled Base64 PNG.
    /// </summary>
    public async Task<PixelArtResult> ConvertToPixelArtAsync(
        Stream imageStream,
        int gridSize = 32,
        IReadOnlyList<string>? hexPalette = null,
        bool applyDithering = false,
        PixelResampleMode resampleMode = PixelResampleMode.Box,
        float brightness = 1.0f,
        float contrast = 1.0f,
        bool invertBrightness = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        if (hexPalette is null || hexPalette.Count == 0)
        {
            throw new ArgumentException("At least one hex palette color is required.", nameof(hexPalette));
        }

        var palette = ParsePalette(hexPalette);
        var clampedGrid = Math.Clamp(gridSize, 4, 256);
        var clampedBrightness = Math.Clamp(brightness, 0.1f, 3.0f);
        var clampedContrast = Math.Clamp(contrast, 0.1f, 3.0f);

        using var source = await Image.LoadAsync<Rgba32>(imageStream, ct);

        // Preserve source aspect ratio while fitting inside gridSize x gridSize.
        var (targetWidth, targetHeight) = CalculateContainedDimensions(source.Width, source.Height, clampedGrid);

        IResampler sampler = resampleMode == PixelResampleMode.NearestNeighbor
            ? KnownResamplers.NearestNeighbor
            : KnownResamplers.Box;

        source.Mutate(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
                Sampler = sampler
            });

            if (Math.Abs(clampedBrightness - 1.0f) > 0.001f)
            {
                ctx.Brightness(clampedBrightness);
            }

            if (Math.Abs(clampedContrast - 1.0f) > 0.001f)
            {
                ctx.Contrast(clampedContrast);
            }

            if (invertBrightness)
            {
                ctx.Invert();
            }
        });

        var hexGrid = new string[targetHeight, targetWidth];
        using var quantized = new Image<Rgba32>(targetWidth, targetHeight);

        if (applyDithering)
        {
            QuantizeWithFloydSteinberg(source, quantized, palette, hexGrid);
        }
        else
        {
            QuantizeDirect(source, quantized, palette, hexGrid);
        }

        // Upscale cleanly using NearestNeighbor so every pixel stays razor-sharp without blur.
        var scaleFactor = Math.Max(1, DefaultMinPreviewDimension / Math.Max(targetWidth, targetHeight));
        var upscaledWidth = targetWidth * scaleFactor;
        var upscaledHeight = targetHeight * scaleFactor;

        using var upscaled = quantized.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(upscaledWidth, upscaledHeight),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.NearestNeighbor
        }));

        await using var pngStream = new MemoryStream();
        await upscaled.SaveAsPngAsync(pngStream, ct);
        var base64Png = Convert.ToBase64String(pngStream.ToArray());

        return new PixelArtResult(
            Width: targetWidth,
            Height: targetHeight,
            UpscaledWidth: upscaledWidth,
            UpscaledHeight: upscaledHeight,
            Base64Png: base64Png,
            HexGrid: hexGrid);
    }

    /// <summary>
    /// Rebuilds PlainText and run-length-batched ColoredHtml from an edited <see cref="AsciiCell"/> grid.
    /// </summary>
    public static (string PlainText, string ColoredHtml) BuildAsciiTextAndHtml(
        AsciiCell[,] grid,
        AsciiColorMode colorMode)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var rows = grid.GetLength(0);
        var cols = grid.GetLength(1);

        var plainBuilder = new StringBuilder(rows * (cols + 1));
        var htmlBuilder = new StringBuilder(rows * cols * 12);

        for (var y = 0; y < rows; y++)
        {
            string? activeRunColor = null;
            var runText = new StringBuilder(cols);

            for (var x = 0; x < cols; x++)
            {
                var cell = grid[y, x];
                plainBuilder.Append(cell.Glyph);

                if (colorMode == AsciiColorMode.Monochrome)
                {
                    runText.Append(cell.Glyph);
                    continue;
                }

                if (activeRunColor is null)
                {
                    activeRunColor = cell.HexColor;
                    runText.Append(cell.Glyph);
                }
                else if (string.Equals(activeRunColor, cell.HexColor, StringComparison.Ordinal))
                {
                    runText.Append(cell.Glyph);
                }
                else
                {
                    AppendColoredSpan(htmlBuilder, activeRunColor, runText);
                    runText.Clear();
                    activeRunColor = cell.HexColor;
                    runText.Append(cell.Glyph);
                }
            }

            if (colorMode == AsciiColorMode.Monochrome)
            {
                htmlBuilder.Append(WebUtility.HtmlEncode(runText.ToString()));
            }
            else if (runText.Length > 0 && activeRunColor is not null)
            {
                AppendColoredSpan(htmlBuilder, activeRunColor, runText);
            }

            if (y < rows - 1)
            {
                plainBuilder.Append('\n');
                htmlBuilder.Append('\n');
            }
        }

        return (plainBuilder.ToString(), htmlBuilder.ToString());
    }

    /// <summary>
    /// Re-renders an edited 2D hex color grid into a crisp NearestNeighbor-upscaled Base64 PNG.
    /// Useful after interactive pixel-paint edits on the canvas.
    /// </summary>
    public static PixelArtResult RenderHexGridToPixelArt(string[,] hexGrid)
    {
        ArgumentNullException.ThrowIfNull(hexGrid);
        var height = hexGrid.GetLength(0);
        var width = hexGrid.GetLength(1);

        using var buffer = new Image<Rgba32>(width, height);
        buffer.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var hex = hexGrid[y, x];
                    row[x] = string.IsNullOrWhiteSpace(hex)
                        ? new Rgba32(0, 0, 0, 0)
                        : ParseHexColor(hex);
                }
            }
        });

        var scaleFactor = Math.Max(1, DefaultMinPreviewDimension / Math.Max(width, height));
        var upscaledWidth = width * scaleFactor;
        var upscaledHeight = height * scaleFactor;

        using var upscaled = buffer.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(upscaledWidth, upscaledHeight),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.NearestNeighbor
        }));

        using var pngStream = new MemoryStream();
        upscaled.SaveAsPng(pngStream);

        return new PixelArtResult(
            Width: width,
            Height: height,
            UpscaledWidth: upscaledWidth,
            UpscaledHeight: upscaledHeight,
            Base64Png: Convert.ToBase64String(pngStream.ToArray()),
            HexGrid: hexGrid);
    }

    /// <summary>
    /// Generates a synthetic retro sample PNG in memory so users can test the studio immediately.
    /// </summary>
    public static byte[] CreateSampleArtworkPng(int size = 128)
    {
        using var img = new Image<Rgba32>(size, size);
        var cx = size / 2.0;
        var cy = size / 2.0;
        var radius = size * 0.36;

        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < size; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < size; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var dist = Math.Sqrt((dx * dx) + (dy * dy));

                    if (dist <= radius)
                    {
                        // Shaded synthwave orb
                        var light = Math.Clamp(1.0 - (Math.Sqrt(Math.Pow(x - cx * 0.72, 2) + Math.Pow(y - cy * 0.68, 2)) / (radius * 1.45)), 0.0, 1.0);
                        var r = (byte)(40 + light * 215);
                        var g = (byte)(20 + light * 165);
                        var b = (byte)(120 + (1.0 - light) * 135);
                        row[x] = new Rgba32(r, g, b, 255);
                    }
                    else
                    {
                        // Deep gradient sky + retro horizon grid
                        var skyT = (double)y / size;
                        var isHorizonLine = y > size * 0.66 && (y % 8 == 0 || (x - (size / 2)) % 12 == 0);
                        if (isHorizonLine)
                        {
                            row[x] = new Rgba32(34, 211, 238, 255);
                        }
                        else
                        {
                            row[x] = new Rgba32(
                                (byte)(12 + skyT * 36),
                                (byte)(14 + skyT * 18),
                                (byte)(28 + skyT * 48),
                                255);
                        }
                    }
                }
            }
        });

        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    // -------------------------------------------------------------------------
    // Unit-Testable Pure Helper Methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes perceptual ITU-R BT.601 luminance: 0.299R + 0.587G + 0.114B.
    /// </summary>
    public static double ComputeLuminance(byte r, byte g, byte b)
        => (0.299 * r) + (0.587 * g) + (0.114 * b);

    /// <summary>
    /// Adjusts output ASCII rows to compensate for terminal characters being ~2x taller than wide.
    /// </summary>
    public static int CalculateAspectAdjustedHeight(
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        double fontAspectRatio = DefaultFontAspectRatio)
    {
        if (sourceWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        if (sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceHeight));
        if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
        if (fontAspectRatio <= 0) throw new ArgumentOutOfRangeException(nameof(fontAspectRatio));

        var aspect = (double)sourceHeight / sourceWidth;
        return Math.Max(1, (int)Math.Round(targetWidth * aspect * fontAspectRatio));
    }

    /// <summary>
    /// Fits source dimensions inside a square maxGridSize bounding box while preserving aspect ratio.
    /// </summary>
    public static (int Width, int Height) CalculateContainedDimensions(int sourceWidth, int sourceHeight, int maxGridSize)
    {
        if (sourceWidth <= 0) throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        if (sourceHeight <= 0) throw new ArgumentOutOfRangeException(nameof(sourceHeight));
        if (maxGridSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxGridSize));

        if (sourceWidth >= sourceHeight)
        {
            var height = Math.Max(1, (int)Math.Round(maxGridSize * ((double)sourceHeight / sourceWidth)));
            return (maxGridSize, height);
        }

        var width = Math.Max(1, (int)Math.Round(maxGridSize * ((double)sourceWidth / sourceHeight)));
        return (width, maxGridSize);
    }

    /// <summary>
    /// Maps luminance [0..255] to a glyph in <paramref name="characterRamp"/>.
    /// Assumes ramp is ordered from densest ink (index 0) to empty/lightest (last index).
    /// </summary>
    public static char MapLuminanceToChar(double luminance, string characterRamp, bool invertBrightness)
    {
        if (string.IsNullOrEmpty(characterRamp))
        {
            throw new ArgumentException("Character ramp cannot be null or empty.", nameof(characterRamp));
        }

        var normalized = Math.Clamp(luminance / 255.0, 0.0, 1.0);
        var position = invertBrightness ? normalized : 1.0 - normalized;
        var index = (int)Math.Round(position * (characterRamp.Length - 1));
        return characterRamp[Math.Clamp(index, 0, characterRamp.Length - 1)];
    }

    /// <summary>
    /// Maps luminance [0..255] to a classic CRT Matrix phosphor green shade.
    /// </summary>
    public static Rgba32 ComputeMatrixGreenColor(double luminance)
    {
        var t = Math.Clamp(luminance / 255.0, 0.0, 1.0);
        var green = (byte)Math.Clamp((int)Math.Round(40 + (t * 215)), 0, 255);
        var red = (byte)Math.Clamp((int)Math.Round(t * 70), 0, 255);
        var blue = (byte)Math.Clamp((int)Math.Round(t * 50), 0, 255);
        return new Rgba32(red, green, blue, 255);
    }

    /// <summary>
    /// Parses a CSS hex color string (#RGB, #RRGGBB, or #RRGGBBAA) into an <see cref="Rgba32"/>.
    /// </summary>
    public static Rgba32 ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new ArgumentException("Hex color cannot be null or empty.", nameof(hex));
        }

        var span = hex.AsSpan().Trim();
        if (span.StartsWith("#"))
        {
            span = span[1..];
        }

        if (span.Length == 3)
        {
            var r = ParseHexNibble(span[0]);
            var g = ParseHexNibble(span[1]);
            var b = ParseHexNibble(span[2]);
            return new Rgba32((byte)(r * 17), (byte)(g * 17), (byte)(b * 17), 255);
        }

        if (span.Length is 6 or 8)
        {
            var r = byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var g = byte.Parse(span.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var b = byte.Parse(span.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var a = span.Length == 8
                ? byte.Parse(span.Slice(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : (byte)255;
            return new Rgba32(r, g, b, a);
        }

        throw new FormatException($"Invalid hex color format: '{hex}'.");
    }

    /// <summary>
    /// Formats an <see cref="Rgba32"/> color as uppercase #RRGGBB.
    /// </summary>
    public static string FormatHexColor(Rgba32 color)
        => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>
    /// Finds the nearest palette color using squared Euclidean RGB distance.
    /// </summary>
    public static Rgba32 FindNearestPaletteColor(byte r, byte g, byte b, ReadOnlySpan<Rgba32> palette)
    {
        if (palette.IsEmpty)
        {
            throw new ArgumentException("Palette cannot be empty.", nameof(palette));
        }

        var bestColor = palette[0];
        var bestDistSq = long.MaxValue;

        for (var i = 0; i < palette.Length; i++)
        {
            var candidate = palette[i];
            long dr = r - candidate.R;
            long dg = g - candidate.G;
            long db = b - candidate.B;
            var distSq = (dr * dr) + (dg * dg) + (db * db);

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestColor = candidate;
                if (distSq == 0)
                {
                    break;
                }
            }
        }

        return bestColor;
    }

    // -------------------------------------------------------------------------
    // Private Conversion Helpers
    // -------------------------------------------------------------------------

    private static (char Glyph, string HexColor) ConvertPixelToAsciiCell(
        Rgba32 pixel,
        string characterRamp,
        bool invertBrightness,
        AsciiColorMode colorMode)
    {
        // Composite semi-transparent pixels over a dark studio canvas (#000000).
        var alphaFactor = pixel.A / 255.0;
        var r = (byte)Math.Round(pixel.R * alphaFactor);
        var g = (byte)Math.Round(pixel.G * alphaFactor);
        var b = (byte)Math.Round(pixel.B * alphaFactor);

        var luminance = ComputeLuminance(r, g, b);
        var glyph = pixel.A < 16 && !invertBrightness
            ? ' '
            : MapLuminanceToChar(luminance, characterRamp, invertBrightness);

        var hexColor = colorMode switch
        {
            AsciiColorMode.Monochrome => "#E2E8F0",
            AsciiColorMode.MatrixGreen => FormatHexColor(ComputeMatrixGreenColor(luminance)),
            AsciiColorMode.FullColorHtml => FormatHexColor(new Rgba32(r, g, b, 255)),
            _ => "#E2E8F0"
        };

        return (glyph, hexColor);
    }

    private static void AppendColoredSpan(StringBuilder htmlBuilder, string hexColor, StringBuilder runText)
    {
        htmlBuilder.Append("<span style=\"color:")
            .Append(hexColor)
            .Append("\">")
            .Append(WebUtility.HtmlEncode(runText.ToString()))
            .Append("</span>");
    }

    private static Rgba32[] ParsePalette(IReadOnlyList<string> hexPalette)
    {
        var parsed = new Rgba32[hexPalette.Count];
        for (var i = 0; i < hexPalette.Count; i++)
        {
            parsed[i] = ParseHexColor(hexPalette[i]);
        }
        return parsed;
    }

    private static void QuantizeDirect(
        Image<Rgba32> source,
        Image<Rgba32> destination,
        Rgba32[] palette,
        string[,] hexGrid)
    {
        source.ProcessPixelRows(destination, (srcAccessor, dstAccessor) =>
        {
            ReadOnlySpan<Rgba32> paletteSpan = palette;

            for (var y = 0; y < srcAccessor.Height; y++)
            {
                var srcRow = srcAccessor.GetRowSpan(y);
                var dstRow = dstAccessor.GetRowSpan(y);

                for (var x = 0; x < srcAccessor.Width; x++)
                {
                    var pixel = srcRow[x];
                    if (pixel.A < 128)
                    {
                        dstRow[x] = new Rgba32(0, 0, 0, 0);
                        hexGrid[y, x] = string.Empty;
                        continue;
                    }

                    var nearest = FindNearestPaletteColor(pixel.R, pixel.G, pixel.B, paletteSpan);
                    dstRow[x] = new Rgba32(nearest.R, nearest.G, nearest.B, 255);
                    hexGrid[y, x] = FormatHexColor(nearest);
                }
            }
        });
    }

    private static void QuantizeWithFloydSteinberg(
        Image<Rgba32> source,
        Image<Rgba32> destination,
        Rgba32[] palette,
        string[,] hexGrid)
    {
        var width = source.Width;
        var height = source.Height;

        var rBuf = new float[height, width];
        var gBuf = new float[height, width];
        var bBuf = new float[height, width];
        var opaque = new bool[height, width];

        source.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var p = row[x];
                    opaque[y, x] = p.A >= 128;
                    rBuf[y, x] = p.R;
                    gBuf[y, x] = p.G;
                    bBuf[y, x] = p.B;
                }
            }
        });

        destination.ProcessPixelRows(dstAccessor =>
        {
            ReadOnlySpan<Rgba32> paletteSpan = palette;

            for (var y = 0; y < height; y++)
            {
                var dstRow = dstAccessor.GetRowSpan(y);

                for (var x = 0; x < width; x++)
                {
                    if (!opaque[y, x])
                    {
                        dstRow[x] = new Rgba32(0, 0, 0, 0);
                        hexGrid[y, x] = string.Empty;
                        continue;
                    }

                    var oldR = (byte)Math.Clamp((int)Math.Round(rBuf[y, x]), 0, 255);
                    var oldG = (byte)Math.Clamp((int)Math.Round(gBuf[y, x]), 0, 255);
                    var oldB = (byte)Math.Clamp((int)Math.Round(bBuf[y, x]), 0, 255);

                    var nearest = FindNearestPaletteColor(oldR, oldG, oldB, paletteSpan);
                    dstRow[x] = new Rgba32(nearest.R, nearest.G, nearest.B, 255);
                    hexGrid[y, x] = FormatHexColor(nearest);

                    var errR = rBuf[y, x] - nearest.R;
                    var errG = gBuf[y, x] - nearest.G;
                    var errB = bBuf[y, x] - nearest.B;

                    // Classic Floyd-Steinberg diffusion kernel:
                    //       *   7/16
                    // 3/16 5/16 1/16
                    DiffuseError(rBuf, gBuf, bBuf, opaque, width, height, x + 1, y, errR, errG, errB, 7f / 16f);
                    DiffuseError(rBuf, gBuf, bBuf, opaque, width, height, x - 1, y + 1, errR, errG, errB, 3f / 16f);
                    DiffuseError(rBuf, gBuf, bBuf, opaque, width, height, x, y + 1, errR, errG, errB, 5f / 16f);
                    DiffuseError(rBuf, gBuf, bBuf, opaque, width, height, x + 1, y + 1, errR, errG, errB, 1f / 16f);
                }
            }
        });
    }

    private static void DiffuseError(
        float[,] rBuf,
        float[,] gBuf,
        float[,] bBuf,
        bool[,] opaque,
        int width,
        int height,
        int nx,
        int ny,
        float errR,
        float errG,
        float errB,
        float weight)
    {
        if (nx < 0 || nx >= width || ny < 0 || ny >= height || !opaque[ny, nx])
        {
            return;
        }

        rBuf[ny, nx] += errR * weight;
        gBuf[ny, nx] += errG * weight;
        bBuf[ny, nx] += errB * weight;
    }

    private static int ParseHexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => 10 + (c - 'a'),
        >= 'A' and <= 'F' => 10 + (c - 'A'),
        _ => throw new FormatException($"Invalid hex digit '{c}'.")
    };
}

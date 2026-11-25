using System.Net;
using System.Text;
using System.Text.Json;
using Pixcii.Data;
using Pixcii.Data.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Pixcii.Services;

public sealed record SpritesheetExportResult(
    byte[] PngBytes,
    int FrameWidth,
    int FrameHeight,
    int FrameCount,
    int SheetWidth,
    int SheetHeight
);

/// <summary>
/// Exports Pixcii ASCII and Pixel Art frames into game-ready formats:
/// Animated GIFs, horizontal PNG spritesheets, C# console animation snippets,
/// structured JSON frame data, raw .txt, and standalone animated .html files.
/// </summary>
public sealed class ExportService
{
    private const int GlyphCellWidth = 6;
    private const int GlyphCellHeight = 10;

    private static readonly JsonSerializerOptions PrettyJsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    // -------------------------------------------------------------------------
    // 1. Animated GIF Export (.gif)
    // -------------------------------------------------------------------------

    public async Task<byte[]> ExportAnimatedGifAsync(
        IReadOnlyList<ArtFrame> frames,
        ProjectMode mode,
        int fps,
        int pixelScale = 4,
        bool transparentBackground = false,
        CancellationToken ct = default)
    {
        if (frames is null || frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required to export a GIF.", nameof(frames));
        }

        var clampedFps = Math.Clamp(fps, 1, 24);
        var defaultDelayCentiseconds = Math.Max(2, (int)Math.Round(100.0 / clampedFps));
        var scale = Math.Clamp(pixelScale, 1, 16);

        var renderedFrames = new List<(Image<Rgba32> Image, int DelayCs)>(frames.Count);

        try
        {
            foreach (var frame in frames)
            {
                ct.ThrowIfCancellationRequested();
                var img = RasterizeFrame(frame, mode, scale, transparentBackground ? null : "#0B0D12");
                var delayCs = frame.DurationMs > 0
                    ? Math.Max(2, (int)Math.Round(frame.DurationMs / 10.0))
                    : defaultDelayCentiseconds;
                renderedFrames.Add((img, delayCs));
            }

            // Normalize all frames to identical canvas dimensions so GIF encoding never mismatches.
            var targetWidth = renderedFrames[0].Image.Width;
            var targetHeight = renderedFrames[0].Image.Height;

            using var gif = new Image<Rgba32>(targetWidth, targetHeight);
            gif.Metadata.GetGifMetadata().RepeatCount = 0; // Loop forever

            for (var i = 0; i < renderedFrames.Count; i++)
            {
                var (frameImg, delayCs) = renderedFrames[i];
                if (frameImg.Width != targetWidth || frameImg.Height != targetHeight)
                {
                    frameImg.Mutate(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(targetWidth, targetHeight),
                        Mode = ResizeMode.Pad,
                        Sampler = KnownResamplers.NearestNeighbor
                    }));
                }

                var frameMeta = frameImg.Frames.RootFrame.Metadata.GetGifMetadata();
                frameMeta.FrameDelay = delayCs;
                frameMeta.DisposalMethod = GifDisposalMethod.RestoreToBackground;

                gif.Frames.AddFrame(frameImg.Frames.RootFrame);
            }

            // Remove the initial blank root frame created by new Image<Rgba32>(w, h).
            if (gif.Frames.Count > renderedFrames.Count)
            {
                gif.Frames.RemoveFrame(0);
            }

            await using var ms = new MemoryStream();
            var encoder = new GifEncoder
            {
                ColorTableMode = GifColorTableMode.Local
            };
            await gif.SaveAsGifAsync(ms, encoder, ct);
            return ms.ToArray();
        }
        finally
        {
            foreach (var (img, _) in renderedFrames)
            {
                img.Dispose();
            }
        }
    }

    // -------------------------------------------------------------------------
    // 2. Game Engine Horizontal Spritesheet Export (.png)
    // -------------------------------------------------------------------------

    public async Task<SpritesheetExportResult> ExportSpritesheetAsync(
        IReadOnlyList<ArtFrame> frames,
        ProjectMode mode,
        int pixelScale = 1,
        string? backgroundHex = null,
        CancellationToken ct = default)
    {
        if (frames is null || frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required to export a spritesheet.", nameof(frames));
        }

        var scale = Math.Clamp(pixelScale, 1, 16);
        var renderedFrames = new List<Image<Rgba32>>(frames.Count);

        try
        {
            foreach (var frame in frames)
            {
                ct.ThrowIfCancellationRequested();
                renderedFrames.Add(RasterizeFrame(frame, mode, scale, backgroundHex));
            }

            var frameWidth = renderedFrames.Max(f => f.Width);
            var frameHeight = renderedFrames.Max(f => f.Height);
            var sheetWidth = frameWidth * renderedFrames.Count;
            var sheetHeight = frameHeight;

            var bg = string.IsNullOrWhiteSpace(backgroundHex)
                ? new Rgba32(0, 0, 0, 0)
                : MediaConversionService.ParseHexColor(backgroundHex);

            using var sheet = new Image<Rgba32>(sheetWidth, sheetHeight, bg);

            sheet.Mutate(ctx =>
            {
                for (var i = 0; i < renderedFrames.Count; i++)
                {
                    ctx.DrawImage(renderedFrames[i], new Point(i * frameWidth, 0), 1f);
                }
            });

            await using var ms = new MemoryStream();
            await sheet.SaveAsPngAsync(ms, ct);

            return new SpritesheetExportResult(
                PngBytes: ms.ToArray(),
                FrameWidth: frameWidth,
                FrameHeight: frameHeight,
                FrameCount: renderedFrames.Count,
                SheetWidth: sheetWidth,
                SheetHeight: sheetHeight);
        }
        finally
        {
            foreach (var img in renderedFrames)
            {
                img.Dispose();
            }
        }
    }

    // -------------------------------------------------------------------------
    // 3. Game-Dev Code Snippets & Text/HTML Exports
    // -------------------------------------------------------------------------

    public string GenerateCSharpConsoleSnippet(
        IReadOnlyList<ArtFrame> frames,
        int fps,
        string projectTitle = "PixciiSprite")
    {
        var delayMs = Math.Max(40, 1000 / Math.Clamp(fps, 1, 24));
        var sb = new StringBuilder();

        sb.AppendLine($"// {projectTitle} — Generated by Pixcii Studio ({fps} FPS)");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine();
        sb.AppendLine("string[] frames =");
        sb.AppendLine("[");

        for (var i = 0; i < frames.Count; i++)
        {
            var plain = ExtractPlainText(frames[i]);
            var escaped = EscapeCSharpString(plain);
            var comma = i < frames.Count - 1 ? "," : string.Empty;
            sb.AppendLine($"    \"{escaped}\"{comma}");
        }

        sb.AppendLine("];");
        sb.AppendLine();
        sb.AppendLine("Console.CursorVisible = false;");
        sb.AppendLine("Console.Clear();");
        sb.AppendLine();
        sb.AppendLine("var index = 0;");
        sb.AppendLine("while (true)");
        sb.AppendLine("{");
        sb.AppendLine("    Console.SetCursorPosition(0, 0);");
        sb.AppendLine("    Console.Write(frames[index]);");
        sb.AppendLine("    index = (index + 1) % frames.Length;");
        sb.AppendLine($"    Thread.Sleep({delayMs});");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public string GenerateJsonFrameData(
        IReadOnlyList<ArtFrame> frames,
        ProjectMode mode,
        int fps,
        string projectTitle = "PixciiSprite")
    {
        var clampedFps = Math.Clamp(fps, 1, 24);
        var frameDelayMs = Math.Max(40, 1000 / clampedFps);

        if (mode == ProjectMode.Ascii)
        {
            var asciiFrames = frames.Select((f, idx) =>
            {
                var parsed = FrameSerialization.DeserializeAscii(
                    f.AsciiContent,
                    MediaConversionService.BuildAsciiTextAndHtml);

                var lines = parsed?.PlainText.Split('\n') ?? [];
                return new
                {
                    index = idx,
                    durationMs = f.DurationMs > 0 ? f.DurationMs : frameDelayMs,
                    columns = parsed?.Columns ?? 0,
                    rows = parsed?.Rows ?? 0,
                    lines,
                    plainText = parsed?.PlainText ?? string.Empty
                };
            }).ToList();

            var doc = new
            {
                title = projectTitle,
                mode = "Ascii",
                fps = clampedFps,
                frameDelayMs,
                width = asciiFrames.FirstOrDefault()?.columns ?? 0,
                height = asciiFrames.FirstOrDefault()?.rows ?? 0,
                frameCount = asciiFrames.Count,
                frames = asciiFrames
            };

            return JsonSerializer.Serialize(doc, PrettyJsonOpts);
        }
        else
        {
            var pixelFrames = frames.Select((f, idx) =>
            {
                var grid = ExtractHexGrid(f);
                var h = grid.GetLength(0);
                var w = grid.GetLength(1);
                var rows = new string[h][];
                for (var r = 0; r < h; r++)
                {
                    rows[r] = new string[w];
                    for (var c = 0; c < w; c++)
                    {
                        rows[r][c] = grid[r, c];
                    }
                }

                return new
                {
                    index = idx,
                    durationMs = f.DurationMs > 0 ? f.DurationMs : frameDelayMs,
                    width = w,
                    height = h,
                    hexGrid = rows
                };
            }).ToList();

            var doc = new
            {
                title = projectTitle,
                mode = "PixelArt",
                fps = clampedFps,
                frameDelayMs,
                width = pixelFrames.FirstOrDefault()?.width ?? 0,
                height = pixelFrames.FirstOrDefault()?.height ?? 0,
                frameCount = pixelFrames.Count,
                frames = pixelFrames
            };

            return JsonSerializer.Serialize(doc, PrettyJsonOpts);
        }
    }

    public string GenerateRawAsciiText(IReadOnlyList<ArtFrame> frames)
    {
        if (frames.Count == 0)
        {
            return string.Empty;
        }

        if (frames.Count == 1)
        {
            return ExtractPlainText(frames[0]);
        }

        var sb = new StringBuilder();
        for (var i = 0; i < frames.Count; i++)
        {
            sb.AppendLine($"=== FRAME {i + 1:D2} ===");
            sb.AppendLine(ExtractPlainText(frames[i]));
            if (i < frames.Count - 1)
            {
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public string GenerateStandaloneAsciiHtml(
        IReadOnlyList<ArtFrame> frames,
        int fps,
        string projectTitle = "Pixcii Art")
    {
        var delayMs = Math.Max(40, 1000 / Math.Clamp(fps, 1, 24));
        var safeTitle = WebUtility.HtmlEncode(projectTitle);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine($"  <title>{safeTitle} — Pixcii Export</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #0b0d12; color: #e2e8f0; font-family: 'JetBrains Mono', ui-monospace, monospace; }");
        sb.AppendLine("    .stage { padding: 24px; border-radius: 10px; border: 1px solid #232838; background: #10131c; box-shadow: 0 20px 60px rgba(0,0,0,0.6); }");
        sb.AppendLine("    pre { margin: 0; font-size: 12px; line-height: 1.15; white-space: pre; }");
        sb.AppendLine("    .frame { display: none; }");
        sb.AppendLine("    .frame.active { display: block; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"stage\">");

        for (var i = 0; i < frames.Count; i++)
        {
            var parsed = FrameSerialization.DeserializeAscii(
                frames[i].AsciiContent,
                MediaConversionService.BuildAsciiTextAndHtml);
            var activeClass = i == 0 ? " active" : string.Empty;
            var htmlBody = parsed?.ColoredHtml ?? WebUtility.HtmlEncode(ExtractPlainText(frames[i]));
            sb.AppendLine($"    <pre class=\"frame{activeClass}\">{htmlBody}</pre>");
        }

        sb.AppendLine("  </div>");

        if (frames.Count > 1)
        {
            sb.AppendLine("  <script>");
            sb.AppendLine("    const frames = document.querySelectorAll('.frame');");
            sb.AppendLine("    let idx = 0;");
            sb.AppendLine($"    setInterval(() => {{ frames[idx].classList.remove('active'); idx = (idx + 1) % frames.length; frames[idx].classList.add('active'); }}, {delayMs});");
            sb.AppendLine("  </script>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Rasterization Helpers (Pixel Art & Built-In Bitmap Font ASCII)
    // -------------------------------------------------------------------------

    private static Image<Rgba32> RasterizeFrame(
        ArtFrame frame,
        ProjectMode mode,
        int pixelScale,
        string? backgroundHex)
    {
        if (mode == ProjectMode.PixelArt)
        {
            return RasterizePixelFrame(frame, pixelScale, backgroundHex);
        }

        return RasterizeAsciiFrame(frame, pixelScale, backgroundHex);
    }

    private static Image<Rgba32> RasterizePixelFrame(
        ArtFrame frame,
        int pixelScale,
        string? backgroundHex)
    {
        var hexGrid = FrameSerialization.DeserializePixelGrid(frame.AsciiContent);
        if (hexGrid is not null)
        {
            var height = hexGrid.GetLength(0);
            var width = hexGrid.GetLength(1);
            var hasBg = !string.IsNullOrWhiteSpace(backgroundHex);
            var bg = hasBg ? MediaConversionService.ParseHexColor(backgroundHex!) : new Rgba32(0, 0, 0, 0);

            using var raw = new Image<Rgba32>(width, height, bg);
            raw.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < width; x++)
                    {
                        var hex = hexGrid[y, x];
                        if (!string.IsNullOrEmpty(hex))
                        {
                            row[x] = MediaConversionService.ParseHexColor(hex);
                        }
                    }
                }
            });

            if (pixelScale == 1)
            {
                return raw.Clone();
            }

            return raw.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width * pixelScale, height * pixelScale),
                Mode = ResizeMode.Stretch,
                Sampler = KnownResamplers.NearestNeighbor
            }));
        }

        if (!string.IsNullOrEmpty(frame.PixelDataBase64))
        {
            var bytes = Convert.FromBase64String(frame.PixelDataBase64);
            var img = Image.Load<Rgba32>(bytes);
            if (!string.IsNullOrWhiteSpace(backgroundHex))
            {
                var bg = MediaConversionService.ParseHexColor(backgroundHex);
                var composited = new Image<Rgba32>(img.Width, img.Height, bg);
                composited.Mutate(ctx => ctx.DrawImage(img, 1f));
                img.Dispose();
                return composited;
            }
            return img;
        }

        return new Image<Rgba32>(32 * pixelScale, 32 * pixelScale, new Rgba32(0, 0, 0, 0));
    }

    private static Image<Rgba32> RasterizeAsciiFrame(
        ArtFrame frame,
        int pixelScale,
        string? backgroundHex)
    {
        var parsed = FrameSerialization.DeserializeAscii(
            frame.AsciiContent,
            MediaConversionService.BuildAsciiTextAndHtml);

        var cols = parsed?.Columns ?? 16;
        var rows = parsed?.Rows ?? 8;

        // Keep ASCII GIF/PNG crisp and reasonably sized regardless of large column counts.
        var cellScale = Math.Clamp(pixelScale / 2, 1, 3);
        var cellW = GlyphCellWidth * cellScale;
        var cellH = GlyphCellHeight * cellScale;

        var bg = string.IsNullOrWhiteSpace(backgroundHex)
            ? new Rgba32(11, 13, 18, 255)
            : MediaConversionService.ParseHexColor(backgroundHex);

        var image = new Image<Rgba32>(cols * cellW, rows * cellH, bg);
        if (parsed is null)
        {
            return image;
        }

        image.ProcessPixelRows(accessor =>
        {
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var cell = parsed.Grid[r, c];
                    if (cell.Glyph == ' ')
                    {
                        continue;
                    }

                    var fg = MediaConversionService.ParseHexColor(cell.HexColor);
                    DrawGlyph(accessor, c * cellW, r * cellH, cell.Glyph, fg, cellScale);
                }
            }
        });

        return image;
    }

    private static string ExtractPlainText(ArtFrame frame)
    {
        var parsed = FrameSerialization.DeserializeAscii(
            frame.AsciiContent,
            MediaConversionService.BuildAsciiTextAndHtml);
        return parsed?.PlainText ?? frame.AsciiContent ?? string.Empty;
    }

    private static string[,] ExtractHexGrid(ArtFrame frame)
    {
        var grid = FrameSerialization.DeserializePixelGrid(frame.AsciiContent);
        if (grid is not null)
        {
            return grid;
        }

        return new string[0, 0];
    }

    private static string EscapeCSharpString(string value)
        => value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "")
            .Replace("\n", "\\n");

    // -------------------------------------------------------------------------
    // Lightweight 5x7 Retro Bitmap Font for Font-Free ASCII Rasterization
    // -------------------------------------------------------------------------

    private static void DrawGlyph(
        PixelAccessor<Rgba32> accessor,
        int originX,
        int originY,
        char glyph,
        Rgba32 color,
        int scale)
    {
        // Shade blocks rendered cleanly as dithered/solid cell fills.
        if (glyph is '█' or '▓' or '▒' or '░')
        {
            var density = glyph switch
            {
                '█' => 1.0,
                '▓' => 0.75,
                '▒' => 0.5,
                _ => 0.25
            };

            var w = GlyphCellWidth * scale;
            var h = GlyphCellHeight * scale;
            for (var dy = 0; dy < h; dy++)
            {
                var py = originY + dy;
                if (py >= accessor.Height) break;
                var row = accessor.GetRowSpan(py);
                for (var dx = 0; dx < w; dx++)
                {
                    var px = originX + dx;
                    if (px >= accessor.Width) break;

                    var on = density >= 1.0
                        || (density >= 0.75 && ((px + py) % 2 == 0 || px % 2 == 0))
                        || (density >= 0.50 && ((px + py) % 2 == 0))
                        || (px % 2 == 0 && py % 2 == 0);

                    if (on)
                    {
                        row[px] = color;
                    }
                }
            }
            return;
        }

        var bitmap = GetBitmap5x7(glyph);
        for (var gy = 0; gy < 7; gy++)
        {
            var bits = bitmap[gy];
            for (var gx = 0; gx < 5; gx++)
            {
                if ((bits & (1 << (4 - gx))) == 0)
                {
                    continue;
                }

                for (var sy = 0; sy < scale; sy++)
                {
                    var py = originY + ((gy + 1) * scale) + sy;
                    if (py >= accessor.Height) continue;
                    var row = accessor.GetRowSpan(py);

                    for (var sx = 0; sx < scale; sx++)
                    {
                        var px = originX + (gx * scale) + sx;
                        if (px < accessor.Width)
                        {
                            row[px] = color;
                        }
                    }
                }
            }
        }
    }

    private static byte[] GetBitmap5x7(char c) => c switch
    {
        '@' => [0b01110, 0b10001, 0b10111, 0b10101, 0b10111, 0b10000, 0b01110],
        '#' => [0b01010, 0b01010, 0b11111, 0b01010, 0b11111, 0b01010, 0b01010],
        '%' => [0b11001, 0b11010, 0b00010, 0b00100, 0b01000, 0b01011, 0b10011],
        '$' => [0b00100, 0b01111, 0b10100, 0b01110, 0b00101, 0b11110, 0b00100],
        '&' => [0b01100, 0b10010, 0b10100, 0b01000, 0b10101, 0b10010, 0b01101],
        '*' => [0b00100, 0b10101, 0b01110, 0b11111, 0b01110, 0b10101, 0b00100],
        '+' => [0b00000, 0b00100, 0b00100, 0b11111, 0b00100, 0b00100, 0b00000],
        '=' => [0b00000, 0b00000, 0b11111, 0b00000, 0b11111, 0b00000, 0b00000],
        '-' => [0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000],
        '_' => [0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b11111],
        ':' => [0b00000, 0b00100, 0b00100, 0b00000, 0b00100, 0b00100, 0b00000],
        ';' => [0b00000, 0b00100, 0b00100, 0b00000, 0b00100, 0b00100, 0b01000],
        '.' => [0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00100, 0b00100],
        ',' => [0b00000, 0b00000, 0b00000, 0b00000, 0b00100, 0b00100, 0b01000],
        '~' => [0b00000, 0b01001, 0b10110, 0b00000, 0b00000, 0b00000, 0b00000],
        '^' => [0b00100, 0b01010, 0b10001, 0b00000, 0b00000, 0b00000, 0b00000],
        '/' => [0b00001, 0b00010, 0b00010, 0b00100, 0b01000, 0b01000, 0b10000],
        '\\' => [0b10000, 0b01000, 0b01000, 0b00100, 0b00010, 0b00010, 0b00001],
        '|' => [0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
        '!' => [0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00000, 0b00100],
        '?' => [0b01110, 0b10001, 0b00010, 0b00100, 0b00100, 0b00000, 0b00100],
        '(' => [0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010],
        ')' => [0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000],
        '[' => [0b01110, 0b01000, 0b01000, 0b01000, 0b01000, 0b01000, 0b01110],
        ']' => [0b01110, 0b00010, 0b00010, 0b00010, 0b00010, 0b00010, 0b01110],
        '{' => [0b00010, 0b00100, 0b00100, 0b01000, 0b00100, 0b00100, 0b00010],
        '}' => [0b01000, 0b00100, 0b00100, 0b00010, 0b00100, 0b00100, 0b01000],
        '<' => [0b00010, 0b00100, 0b01000, 0b10000, 0b01000, 0b00100, 0b00010],
        '>' => [0b01000, 0b00100, 0b00010, 0b00001, 0b00010, 0b00100, 0b01000],
        '0' or 'O' or 'o' => [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
        '1' or 'I' or 'l' or 'i' => [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        'B' or '8' => [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
        'M' or 'W' or 'm' or 'w' => [0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001],
        'Q' or 'q' => [0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101],
        'C' or 'c' => [0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110],
        'U' or 'u' => [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        'X' or 'x' => [0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001],
        'Y' or 'y' => [0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100],
        'Z' or 'z' => [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111],
        'a' or 'd' or 'b' or 'p' => [0b00000, 0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b01110],
        'v' or 'r' or 'n' => [0b00000, 0b10001, 0b10001, 0b10001, 0b01010, 0b01010, 0b00100],
        'h' or 'k' => [0b10000, 0b10000, 0b10110, 0b11001, 0b10001, 0b10001, 0b10001],
        'f' or 't' or 'j' => [0b00110, 0b01000, 0b11100, 0b01000, 0b01000, 0b01000, 0b01000],
        _ => [0b01110, 0b10001, 0b10101, 0b10101, 0b10001, 0b10001, 0b01110]
    };
}

namespace Pixcii.Data;

/// <summary>
/// Colorization strategy for Image-to-ASCII output.
/// </summary>
public enum AsciiColorMode
{
    Monochrome = 0,
    MatrixGreen = 1,
    FullColorHtml = 2
}

/// <summary>
/// Resampling filter used when downsampling images into a pixel-art grid.
/// </summary>
public enum PixelResampleMode
{
    Box = 0,
    NearestNeighbor = 1
}

/// <summary>
/// Built-in ASCII character ramps ordered from densest ink to lightest/empty.
/// </summary>
public static class AsciiRamps
{
    public const string Standard = "@%#*+=-:. ";
    public const string Dense = "$@B%8&WM#*oahkbdpqwmZO0QLCJUYXzcvunxrjft/\\|()1{}[]?-_+~<>i!lI;:,\"^`'. ";
    public const string Blocks = "█▓▒░ ";
    public const string Minimal = "#+-. ";
}

/// <summary>
/// Single character cell with its resolved CSS hex color.
/// </summary>
public readonly record struct AsciiCell(char Glyph, string HexColor);

/// <summary>
/// Output DTO from the Image-to-ASCII pipeline.
/// </summary>
public sealed record AsciiResult(
    int Columns,
    int Rows,
    string PlainText,
    string ColoredHtml,
    AsciiCell[,] Grid
);

/// <summary>
/// Output DTO from the Image-to-Pixel Art pipeline.
/// </summary>
public sealed record PixelArtResult(
    int Width,
    int Height,
    int UpscaledWidth,
    int UpscaledHeight,
    string Base64Png,
    string[,] HexGrid
)
{
    public string DataUrl => $"data:image/png;base64,{Base64Png}";
}

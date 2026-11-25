namespace Pixcii.Data;

public enum StudioTool
{
    Pencil,
    Eraser,
    FillBucket,
    AsciiBrush,
    Eyedropper,
    Marquee
}

public enum CanvasMode
{
    Pixel,
    Ascii,
    Hybrid
}

public sealed record CanvasMetadata(
    string ProjectName = "untitled-01.pxc",
    int Width = 32,
    int Height = 32,
    int Fps = 12,
    CanvasMode Mode = CanvasMode.Hybrid
);

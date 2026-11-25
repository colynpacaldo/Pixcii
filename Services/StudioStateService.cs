using Pixcii.Data;

namespace Pixcii.Services;

public sealed class StudioStateService
{
    public event Action? OnChange;

    public StudioTool ActiveTool { get; private set; } = StudioTool.Pencil;
    public CanvasMetadata Metadata { get; private set; } = new();
    public string PrimaryColor { get; private set; } = "#22D3EE";
    public char ActiveAsciiGlyph { get; private set; } = '@';
    public int ZoomPercent { get; private set; } = 1200;
    public bool ShowGrid { get; private set; } = true;

    public void SelectTool(StudioTool tool)
    {
        ActiveTool = tool;
        NotifyStateChanged();
    }

    public void SetMode(CanvasMode mode)
    {
        Metadata = Metadata with { Mode = mode };
        NotifyStateChanged();
    }

    public void SetActiveGlyph(char glyph)
    {
        ActiveAsciiGlyph = glyph;
        NotifyStateChanged();
    }

    public void SetPrimaryColor(string hex)
    {
        PrimaryColor = hex;
        NotifyStateChanged();
    }

    public void ToggleGrid()
    {
        ShowGrid = !ShowGrid;
        NotifyStateChanged();
    }

    public void SetZoom(int zoomPercent)
    {
        ZoomPercent = Math.Clamp(zoomPercent, 100, 3200);
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

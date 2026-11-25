using Pixcii.Data;
using Pixcii.Data.Models;

namespace Pixcii.Services;

public sealed class StudioStateService
{
    public event Action? OnChange;
    public event Action<int>? OnFrameSelected;

    private readonly List<ArtFrame> _frames = [];

    public StudioTool ActiveTool { get; private set; } = StudioTool.Pencil;
    public CanvasMetadata Metadata { get; private set; } = new();
    public string PrimaryColor { get; private set; } = "#22D3EE";
    public char ActiveAsciiGlyph { get; private set; } = '@';
    public int ZoomPercent { get; private set; } = 1200;
    public bool ShowGrid { get; private set; } = true;

    // Live Canvas Snapshot (used by Export when timeline is empty)
    public ProjectMode CurrentProjectMode { get; private set; } = ProjectMode.Ascii;
    public AsciiResult? CurrentAscii { get; private set; }
    public PixelArtResult? CurrentPixel { get; private set; }

    // Animation Timeline State
    public IReadOnlyList<ArtFrame> Frames => _frames;
    public int ActiveFrameIndex { get; private set; } = -1;
    public int Fps { get; private set; } = 8;
    public bool IsPlaying { get; private set; }
    public bool OnionSkinningEnabled { get; private set; }

    public ArtFrame? ActiveFrame =>
        ActiveFrameIndex >= 0 && ActiveFrameIndex < _frames.Count
            ? _frames[ActiveFrameIndex]
            : null;

    public ArtFrame? PreviousFrame =>
        ActiveFrameIndex > 0 && ActiveFrameIndex - 1 < _frames.Count
            ? _frames[ActiveFrameIndex - 1]
            : null;

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

    public void SetActiveCanvas(ProjectMode mode, AsciiResult? ascii, PixelArtResult? pixel)
    {
        CurrentProjectMode = mode;
        CurrentAscii = ascii;
        CurrentPixel = pixel;
    }

    // -------------------------------------------------------------------------
    // Frame Management Actions
    // -------------------------------------------------------------------------

    public void AddFrame(ArtFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var insertAt = ActiveFrameIndex >= 0 ? ActiveFrameIndex + 1 : _frames.Count;
        frame.Id = frame.Id == Guid.Empty ? Guid.NewGuid() : frame.Id;
        frame.DurationMs = Math.Max(40, 1000 / Fps);

        _frames.Insert(insertAt, frame);
        NormalizeOrderIndices();
        ActiveFrameIndex = insertAt;

        NotifyStateChanged();
        OnFrameSelected?.Invoke(ActiveFrameIndex);
    }

    public void UpdateActiveFrameContent(string? asciiContent, string? pixelDataBase64)
    {
        if (ActiveFrame is not { } current)
        {
            return;
        }

        current.AsciiContent = asciiContent;
        current.PixelDataBase64 = pixelDataBase64;
        NotifyStateChanged();
    }

    public void DuplicateFrame(int index)
    {
        if (index < 0 || index >= _frames.Count)
        {
            return;
        }

        var source = _frames[index];
        var copy = new ArtFrame
        {
            Id = Guid.NewGuid(),
            StudioProjectId = source.StudioProjectId,
            AsciiContent = source.AsciiContent,
            PixelDataBase64 = source.PixelDataBase64,
            DurationMs = source.DurationMs
        };

        _frames.Insert(index + 1, copy);
        NormalizeOrderIndices();
        ActiveFrameIndex = index + 1;

        NotifyStateChanged();
        OnFrameSelected?.Invoke(ActiveFrameIndex);
    }

    public void DeleteFrame(int index)
    {
        if (index < 0 || index >= _frames.Count)
        {
            return;
        }

        _frames.RemoveAt(index);
        NormalizeOrderIndices();

        if (_frames.Count == 0)
        {
            IsPlaying = false;
            ActiveFrameIndex = -1;
        }
        else
        {
            ActiveFrameIndex = Math.Clamp(index, 0, _frames.Count - 1);
            OnFrameSelected?.Invoke(ActiveFrameIndex);
        }

        NotifyStateChanged();
    }

    public void MoveFrameLeft(int index)
    {
        if (index <= 0 || index >= _frames.Count)
        {
            return;
        }

        (_frames[index - 1], _frames[index]) = (_frames[index], _frames[index - 1]);
        NormalizeOrderIndices();
        ActiveFrameIndex = index - 1;
        NotifyStateChanged();
    }

    public void MoveFrameRight(int index)
    {
        if (index < 0 || index >= _frames.Count - 1)
        {
            return;
        }

        (_frames[index], _frames[index + 1]) = (_frames[index + 1], _frames[index]);
        NormalizeOrderIndices();
        ActiveFrameIndex = index + 1;
        NotifyStateChanged();
    }

    public void ReplaceFrames(IEnumerable<ArtFrame> frames)
    {
        _frames.Clear();
        _frames.AddRange(frames);
        NormalizeOrderIndices();
        ActiveFrameIndex = _frames.Count > 0 ? 0 : -1;

        NotifyStateChanged();
        if (ActiveFrameIndex >= 0)
        {
            OnFrameSelected?.Invoke(ActiveFrameIndex);
        }
    }

    public void SelectFrame(int index)
    {
        if (index < 0 || index >= _frames.Count)
        {
            return;
        }

        ActiveFrameIndex = index;
        NotifyStateChanged();
        OnFrameSelected?.Invoke(ActiveFrameIndex);
    }

    public void StepForward()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        ActiveFrameIndex = (ActiveFrameIndex + 1) % _frames.Count;
        NotifyStateChanged();
        OnFrameSelected?.Invoke(ActiveFrameIndex);
    }

    public void StepBackward()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        ActiveFrameIndex = ActiveFrameIndex <= 0 ? _frames.Count - 1 : ActiveFrameIndex - 1;
        NotifyStateChanged();
        OnFrameSelected?.Invoke(ActiveFrameIndex);
    }

    public void SetFps(int fps)
    {
        Fps = Math.Clamp(fps, 1, 24);
        Metadata = Metadata with { Fps = Fps };

        var durationMs = Math.Max(40, 1000 / Fps);
        foreach (var frame in _frames)
        {
            frame.DurationMs = durationMs;
        }

        NotifyStateChanged();
    }

    public void SetPlaying(bool isPlaying)
    {
        IsPlaying = isPlaying && _frames.Count > 1;
        NotifyStateChanged();
    }

    public void ToggleOnionSkinning()
    {
        OnionSkinningEnabled = !OnionSkinningEnabled;
        NotifyStateChanged();
    }

    private void NormalizeOrderIndices()
    {
        for (var i = 0; i < _frames.Count; i++)
        {
            _frames[i].OrderIndex = i;
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

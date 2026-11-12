using System.Globalization;
using System.Numerics;
using MASS4.Profiling;

namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    const double BudgetMilliseconds = 1000d / 60;
    const int MinimumFrameSlots = 120;
    const int RecordedFrames = 300;
    const int RecordedScopes = 100_000;
    readonly List<FrameRecord> _frames = [];
    int _recordedScopes;
    Guid _recordedSource;
    FrameRecord? _pinnedFrame;
    (ProfileCapture? Capture, FrameRecord? Record) _captureRecord;
    int _hoveredFrame = -1;

    /// <summary>Frame shown by the strip and flame chart: a timing-only pick, else the selected capture.</summary>
    FrameRecord? SelectedFrame
    {
        get
        {
            if (_pinnedFrame is not null || Capture is not { } capture)
            {
                return _pinnedFrame;
            }
            if (!ReferenceEquals(_captureRecord.Capture, capture))
            {
                _captureRecord = (capture, _frames.FirstOrDefault(frame => frame.Source == capture.Source.Id
                    && frame.Generation == capture.Generation) ?? FrameRecord.From(capture));
            }
            return _captureRecord.Record;
        }
    }

    /// <summary>
    /// Whether a live frame slower than <see cref="SlowFrameMilliseconds"/> pauses the attached target and selects
    /// that frame. It only fires while following live frames; Resume or Live re-arms it.
    /// </summary>
    public bool PauseOnSlowFrame { get; set; }

    /// <summary>Threshold for <see cref="PauseOnSlowFrame"/>; defaults to the 60 fps budget.</summary>
    public double SlowFrameMilliseconds { get; set; } = BudgetMilliseconds;

    /// <summary>Appends newly completed frames of the selected source, bounded by frame and scope counts.</summary>
    void RecordFrames()
    {
        if (_recordedSource != _sourceId)
        {
            _frames.Clear();
            _recordedScopes = 0;
            _recordedSource = _sourceId;
            _hoveredFrame = -1;
        }
        // The first scan of a source holds old frames, such as startup, so it never triggers a pause.
        var slow = AppendFrames(PauseOnSlowFrame && FollowLatest && _frames.Count > 0);
        while (_frames.Count > RecordedFrames || (_recordedScopes > RecordedScopes && _frames.Count > 1))
        {
            _recordedScopes -= _frames[0].Scopes.Length;
            _frames.RemoveAt(0);
        }
        if (slow is not null)
        {
            PauseOn(slow);
        }
    }

    /// <summary>Records captures newer than the last record; returns the first one over the threshold when armed.</summary>
    FrameRecord? AppendFrames(bool armed)
    {
        FrameRecord? slow = null;
        var last = _frames.Count > 0 ? _frames[^1].Generation : 0;
        foreach (var capture in _history.Where(capture => capture.Generation > last))
        {
            var record = FrameRecord.From(capture);
            _frames.Add(record);
            _recordedScopes += record.Scopes.Length;
            if (armed && slow is null && record.Milliseconds > SlowFrameMilliseconds)
            {
                slow = record;
            }
        }
        return slow;
    }

    void PauseOn(FrameRecord frame)
    {
        if (Target is { } target)
        {
            target.Controller.Paused = true;
        }
        SelectFrame(frame.Generation);
        Message = string.Create(CultureInfo.InvariantCulture,
            $"Paused on frame #{frame.Frame}: {frame.Milliseconds:F2} ms > {SlowFrameMilliseconds:F2} ms.");
    }

    void SelectFrame(string? key)
    {
        if (long.TryParse(key, CultureInfo.InvariantCulture, out var generation))
        {
            SelectFrame(generation);
        }
    }

    /// <summary>Selects a frame by generation: its full capture while retained, else its timing only.</summary>
    void SelectFrame(long generation)
    {
        if (_history.FirstOrDefault(frame => frame.Generation == generation) is { } capture)
        {
            SelectCapture(capture);
        }
        else if (_frames.FirstOrDefault(frame => frame.Generation == generation) is { } record)
        {
            Capture = null;
            _pinnedFrame = record;
            FollowLatest = false;
            SelectedObject = null;
            _trees.Clear();
        }
    }

    /// <summary>Per-frame overview: one stacked bar per completed frame; clicking a bar selects that frame.</summary>
    void FrameStrip()
    {
        StripHeader();
        using (Gui.StyledNode("profiler-frames", id: "profiler-frames").ExpandWidth().Height(72).Enter())
        {
            var strip = Gui.GetInteractable();
            if (Gui.Pass != Pass.Pass2Render || _frames.Count == 0)
            {
                return;
            }
            var area = Gui.CurrentNode.Rect;
            var hovered = strip.OnHover() ? FrameAt(area, Gui.Input.MousePosition.X) : -1;
            if (hovered >= 0 && strip.OnClick())
            {
                Command("frame", _frames[hovered].Generation.ToString(CultureInfo.InvariantCulture));
            }
            if (hovered != _hoveredFrame)
            {
                _hoveredFrame = hovered;
                Gui.RequestFrame();
            }
            DrawFrames(area, hovered);
        }
    }

    /// <summary>Slow-frame pause control, then the latest viewer message or the strip summary.</summary>
    void StripHeader()
    {
        using (Gui.StyledNode("profiler-toolbar").ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Enter())
        {
            PauseOnSlowFrame = Gui.Checkbox(PauseOnSlowFrame, "Pause when a frame exceeds", id: "profiler-slow-pause");
            var limit = SlowFrameMilliseconds;
            if (Gui.NumberField(ref limit, step: .5, min: .01, max: 10_000, width: 72, format: "0.##", id: "profiler-slow-limit"))
            {
                SlowFrameMilliseconds = limit;
            }
            Gui.Label(Message is null ? $"ms · {FrameSummary()}" : $"ms · {Message}");
        }
    }

    string FrameSummary()
    {
        if (_frames.Count == 0)
        {
            return "Waiting for completed frames.";
        }
        var durations = _frames.Select(static frame => frame.Milliseconds).ToArray();
        var worst = Array.IndexOf(durations, durations.Max());
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{_frames.Count} frames · avg {durations.Average():F2} ms · p95 {Percentile(durations, .95):F2} ms · worst {durations[worst]:F2} ms (#{_frames[worst].Frame})");
        if (SelectedFrame is { } selected)
        {
            text += string.Create(CultureInfo.InvariantCulture, $" · selected #{selected.Frame} {selected.Milliseconds:F2} ms");
        }
        if (_hoveredFrame >= 0 && _hoveredFrame < _frames.Count)
        {
            var hovered = _frames[_hoveredFrame];
            text += string.Create(CultureInfo.InvariantCulture, $" · hover #{hovered.Frame} {hovered.Milliseconds:F2} ms");
        }
        return text;
    }

    static double Percentile(double[] values, double fraction)
    {
        var sorted = values.Order().ToArray();
        return sorted[Math.Clamp((int)Math.Ceiling(fraction * sorted.Length) - 1, 0, sorted.Length - 1)];
    }

    float SlotWidth(Rect area) => area.W / Math.Max(MinimumFrameSlots, _frames.Count);

    /// <summary>Recorded frame index under a pointer x; newest frames are right-aligned.</summary>
    int FrameAt(Rect area, float x)
    {
        var slot = SlotWidth(area);
        var first = area.X + area.W - _frames.Count * slot;
        var index = (int)Math.Floor((x - first) / slot);
        return index >= 0 && index < _frames.Count ? index : -1;
    }

    void DrawFrames(Rect area, int hovered)
    {
        var durations = _frames.Select(static frame => frame.Milliseconds).ToArray();
        // Scale to typical frames so one startup outlier cannot flatten the strip; taller bars clip with a hitch cap.
        var scale = Math.Max(Percentile(durations, .9) * 1.5, .05);
        var pixels = (area.H - 4) / scale;
        var slot = SlotWidth(area);
        var first = area.X + area.W - _frames.Count * slot;
        var untracked = StyleColor("profiler-untracked", "background-color");
        var hitch = StyleColor("profiler-hitch", "background-color");
        var selected = SelectedFrame;
        for (var index = 0; index < _frames.Count; index++)
        {
            var x = first + index * slot;
            if (index == hovered)
            {
                Gui.DrawRect(new Rect(x, area.Y, slot, area.H), StyleColor("profiler-hover", "background-color"));
            }
            var height = (float)Math.Min(area.H - 4, durations[index] * pixels);
            var bar = new Rect(x + .5f, area.Y + area.H - height, Math.Max(1, slot - 1), height);
            Gui.DrawRect(bar, untracked);
            DrawFrameSegments(_frames[index], bar, pixels);
            if (durations[index] > Math.Min(BudgetMilliseconds, scale))
            {
                Gui.DrawRect(new Rect(bar.X, bar.Y, bar.W, 3), hitch);
            }
            if (ReferenceEquals(_frames[index], selected))
            {
                Gui.DrawRectBorder(new Rect(x - .5f, area.Y + 1, slot + 1, area.H - 2),
                    StyleColor("profiler-selection", "border-color"), 1.5f);
            }
        }
        DrawStripGuide(area, scale, pixels);
    }

    void DrawStripGuide(Rect area, double scale, double pixels)
    {
        var guide = StyleColor("profiler-budget", "border-color");
        if (BudgetMilliseconds < scale)
        {
            var budget = area.Y + area.H - (float)(BudgetMilliseconds * pixels);
            Gui.DrawLine(new Vector2(area.X, budget), new Vector2(area.X + area.W, budget), guide);
        }
        var label = Gui.LayoutText(string.Create(CultureInfo.InvariantCulture,
            $"{scale:0.###} ms{(BudgetMilliseconds < scale ? " · line = 60 fps budget" : "")}"), size: 10);
        Gui.DrawRect(new Rect(area.X + 2, area.Y + 2, label.Width + 6, label.Height + 2),
            StyleColor("profiler-frames", "background-color"), 2);
        Gui.DrawTextLayout(label, new Vector2(area.X + 5, area.Y + 3), guide);
    }

    /// <summary>Stacks the frame's top-level scopes of its first track bottom-up in recorded order.</summary>
    void DrawFrameSegments(FrameRecord frame, Rect bar, double pixels)
    {
        if (frame.Scopes.Length == 0)
        {
            return;
        }
        var lane = frame.Scopes.Min(static entry => entry.Track);
        var y = bar.Y + bar.H;
        foreach (var scope in frame.Scopes.Where(entry => entry.ParentId == 0 && entry.Track == lane))
        {
            var height = (float)Math.Min(y - bar.Y, frame.ToMilliseconds(scope.End - scope.Start) * pixels);
            if (height <= 0)
            {
                break;
            }
            y -= height;
            Gui.DrawRect(new Rect(bar.X, y, bar.W, height), SeriesColor(scope.Name));
        }
    }
}

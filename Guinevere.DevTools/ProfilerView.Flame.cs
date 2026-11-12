using System.Globalization;
using System.Numerics;
using Guinevere.Profiling;

namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    const float ScopeRowHeight = 18;
    const float RulerHeight = 16;
    static readonly string[] SeriesSelectors = ["profiler-series-0", "profiler-series-1", "profiler-series-2",
        "profiler-series-3", "profiler-series-4", "profiler-series-5", "profiler-series-6", "profiler-series-7"];
    readonly Dictionary<string, int> _series = new(StringComparer.Ordinal);
    FrameRecord? _flameFrame;
    FlameScope[] _flame = [];
    int _flameRows;
    string? _hoveredScope;

    /// <summary>One completed scope placed on a flame-chart row, with time not covered by its children.</summary>
    readonly record struct FlameScope(ProfileEvent Event, int Row, long Self);

    /// <summary>Selected frame as nested horizontal bars: one lane per track, one row per nesting depth.</summary>
    void FlameChart(FrameRecord frame)
    {
        PrepareFlame(frame);
        Gui.Label(_hoveredScope ?? "Hover a scope for its timing; click to select it for copying.");
        var height = RulerHeight + Math.Max(1, _flameRows) * ScopeRowHeight + 4;
        using (Gui.StyledNode("profiler-flame", id: "profiler-timeline").ExpandWidth().Height(height).Enter())
        {
            var chart = Gui.GetInteractable();
            if (Gui.Pass != Pass.Pass2Render)
            {
                return;
            }
            var area = Gui.CurrentNode.Rect;
            DrawRuler(frame, area);
            var hovered = TrackScopePointer(frame, area, chart);
            for (var index = 0; index < _flame.Length; index++)
            {
                DrawScope(frame, area, _flame[index], index == hovered);
            }
        }
    }

    /// <summary>Updates hover text and click selection; returns the hovered scope index or -1.</summary>
    int TrackScopePointer(FrameRecord frame, Rect area, InteractableElement chart)
    {
        var hovered = chart.OnHover() ? ScopeAt(frame, area, Gui.Input.MousePosition) : -1;
        var text = hovered >= 0 ? ScopeText(frame, _flame[hovered]) : null;
        if (text is not null && chart.OnClick())
        {
            _selectedText = text;
        }
        if (text != _hoveredScope)
        {
            _hoveredScope = text;
            Gui.RequestFrame();
        }
        return hovered;
    }

    void PrepareFlame(FrameRecord frame)
    {
        if (ReferenceEquals(frame, _flameFrame))
        {
            return;
        }
        var scopes = frame.Scopes;
        var byId = scopes.ToDictionary(static entry => entry.Id);
        var depths = new Dictionary<long, int>();
        int Depth(ProfileEvent entry)
        {
            if (depths.TryGetValue(entry.Id, out var known))
            {
                return known;
            }
            depths[entry.Id] = 0;
            var depth = byId.TryGetValue(entry.ParentId, out var parent) && parent.Track == entry.Track
                ? Depth(parent) + 1 : 0;
            return depths[entry.Id] = depth;
        }
        var offsets = new Dictionary<int, int>();
        var rows = 0;
        foreach (var track in scopes.Select(static entry => entry.Track).Distinct().Order())
        {
            offsets[track] = rows;
            rows += scopes.Where(entry => entry.Track == track).Max(Depth) + 1;
        }
        var children = scopes.Where(entry => byId.ContainsKey(entry.ParentId))
            .GroupBy(static entry => entry.ParentId)
            .ToDictionary(static group => group.Key, static group => group.Sum(static entry => entry.End - entry.Start));
        _flame = [.. scopes.Select(entry => new FlameScope(entry, offsets[entry.Track] + Depth(entry),
            entry.End - entry.Start - children.GetValueOrDefault(entry.Id)))];
        _flameRows = rows;
        _flameFrame = frame;
        _hoveredScope = null;
    }

    static Rect ScopeRect(FrameRecord frame, Rect area, FlameScope scope)
    {
        var duration = (double)Math.Max(1, frame.End - frame.Start);
        var x = area.X + (float)((scope.Event.Start - frame.Start) / duration) * area.W;
        var width = (float)((scope.Event.End - scope.Event.Start) / duration) * area.W;
        return new Rect(x, area.Y + RulerHeight + scope.Row * ScopeRowHeight, Math.Max(1, width - 1), ScopeRowHeight - 2);
    }

    /// <summary>Scope under the pointer; very narrow scopes keep a 3-pixel hit width.</summary>
    int ScopeAt(FrameRecord frame, Rect area, Vector2 pointer)
    {
        for (var index = _flame.Length - 1; index >= 0; index--)
        {
            var rect = ScopeRect(frame, area, _flame[index]);
            if (pointer.X >= rect.X && pointer.X <= rect.X + Math.Max(3, rect.W)
                && pointer.Y >= rect.Y && pointer.Y <= rect.Y + rect.H)
            {
                return index;
            }
        }
        return -1;
    }

    static string ScopeText(FrameRecord frame, FlameScope scope)
    {
        var ticks = scope.Event.End - scope.Event.Start;
        var share = 100d * ticks / Math.Max(1, frame.End - frame.Start);
        return string.Create(CultureInfo.InvariantCulture,
            $"{scope.Event.Name} · track {scope.Event.Track} · {frame.ToMilliseconds(ticks):F3} ms total · {frame.ToMilliseconds(scope.Self):F3} ms self · {share:F1}% of frame #{frame.Frame}");
    }

    void DrawScope(FrameRecord frame, Rect area, FlameScope scope, bool hovered)
    {
        var rect = ScopeRect(frame, area, scope);
        Gui.DrawRect(rect, SeriesColor(scope.Event.Name), 2);
        if (hovered)
        {
            Gui.DrawRectBorder(rect, StyleColor("profiler-selection", "border-color"), 1.5f);
        }
        var milliseconds = frame.ToMilliseconds(scope.Event.End - scope.Event.Start);
        string[] labels = [string.Create(CultureInfo.InvariantCulture, $"{scope.Event.Name} {milliseconds:F2} ms"), scope.Event.Name];
        foreach (var label in labels)
        {
            var layout = Gui.LayoutText(label, size: 11);
            if (layout.Width + 6 <= rect.W)
            {
                Gui.DrawTextLayout(layout, new Vector2(rect.X + 3, rect.Y + (rect.H - layout.Height) / 2),
                    StyleColor("profiler-scope-label", "color"));
                return;
            }
        }
    }

    /// <summary>Millisecond grid with the smallest round step that keeps labels at least 70 pixels apart.</summary>
    void DrawRuler(FrameRecord frame, Rect area)
    {
        var total = Math.Max(frame.Milliseconds, 1e-9);
        double[] steps = [.001, .002, .005, .01, .02, .05, .1, .2, .5, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000];
        var step = steps.FirstOrDefault(value => value / total * area.W >= 70, steps[^1]);
        var color = StyleColor("profiler-budget", "border-color");
        for (var tick = 0d; tick <= total; tick += step)
        {
            var x = area.X + (float)(tick / total) * area.W;
            Gui.DrawLine(new Vector2(x, area.Y), new Vector2(x, area.Y + area.H), color, .5f);
            var label = Gui.LayoutText(string.Create(CultureInfo.InvariantCulture, $"{tick:0.###} ms"), size: 10);
            Gui.DrawTextLayout(label, new Vector2(x + 3, area.Y + 1), color);
        }
    }

    /// <summary>Stable color per scope name for this viewer, drawn from the skin's eight series selectors.</summary>
    Color SeriesColor(string name)
    {
        if (!_series.TryGetValue(name, out var index))
        {
            _series[name] = index = _series.Count % SeriesSelectors.Length;
        }
        return StyleColor(SeriesSelectors[index], "background-color");
    }

    Color StyleColor(string selector, string property) =>
        Gui.ResolveStyle(selector).GetColor(property) ?? Color.Transparent;
}

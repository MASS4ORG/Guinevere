using System.Globalization;
using System.Numerics;
using Guinevere.Charts;
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
    TimelineSpan[] _flameIntervals = [];
    int _flameRows;
    int _hoveredFlameIndex = -1;
    string? _hoveredScope;

    /// <summary>One completed scope placed on a flame-chart row, with time not covered by its children.</summary>
    readonly record struct FlameScope(ProfileEvent Event, long Self);

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
            var plot = new TimelinePlot(new Rect(area.X, area.Y + RulerHeight, area.W, area.H - RulerHeight),
                new ChartRange(0, Math.Max(1, frame.End - frame.Start)), ScopeRowHeight);
            var hovered = TrackScopePointer(frame, plot, chart);
            for (var index = 0; index < _flame.Length; index++)
            {
                var scope = _flame[index];
                var span = _flameIntervals[index] with { Color = SeriesColor(scope.Event.Name) };
                if (TimelineChart.DrawInterval(Gui, plot, span, out var bounds, horizontalGap: 1, rowGap: 2, radius: 2))
                {
                    DrawScope(frame, bounds, scope, index == hovered);
                }
            }
        }
    }

    /// <summary>Updates hover text and click selection; returns the hovered scope index or -1.</summary>
    int TrackScopePointer(FrameRecord frame, TimelinePlot plot, InteractableElement chart)
    {
        var hovered = chart.OnHover() ? TimelineChart.HitTest(plot, _flameIntervals, Gui.Input.MousePosition,
            minimumWidth: 3, horizontalGap: 1, rowGap: 2) : -1;
        if (hovered != _hoveredFlameIndex)
        {
            _hoveredFlameIndex = hovered;
            _hoveredScope = hovered >= 0 ? ScopeText(frame, _flame[hovered]) : null;
            Gui.RequestFrame();
        }
        if (_hoveredScope is { } text && chart.OnClick())
        {
            _selectedText = text;
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
        var byId = new Dictionary<long, int>(scopes.Length);
        for (var i = 0; i < scopes.Length; i++)
        {
            byId[scopes[i].Id] = i;
        }
        var parents = new int[scopes.Length];
        var depths = new int[scopes.Length];
        var children = new long[scopes.Length];
        for (var i = 0; i < scopes.Length; i++)
        {
            var scope = scopes[i];
            parents[i] = -1;
            if (byId.TryGetValue(scope.ParentId, out var parent))
            {
                children[parent] += scope.End - scope.Start;
                if (scope.Track == scopes[parent].Track)
                {
                    parents[i] = parent;
                }
            }
        }
        if (!FlameLayout.TryCalculateDepths(parents, depths))
        {
            depths.AsSpan().Clear();
        }
        var offsets = TrackOffsets(scopes, depths, out _flameRows);
        _flame = new FlameScope[scopes.Length];
        _flameIntervals = new TimelineSpan[scopes.Length];
        for (var i = 0; i < scopes.Length; i++)
        {
            var scope = scopes[i];
            var row = offsets[scope.Track] + depths[i];
            _flame[i] = new FlameScope(scope, scope.End - scope.Start - children[i]);
            _flameIntervals[i] = new TimelineSpan(scope.Start - frame.Start, scope.End - frame.Start, row, default);
        }
        _flameFrame = frame;
        _hoveredFlameIndex = -1;
        _hoveredScope = null;
    }

    static Dictionary<int, int> TrackOffsets(ReadOnlySpan<ProfileEvent> scopes, ReadOnlySpan<int> depths, out int rows)
    {
        var offsets = new Dictionary<int, int>();
        for (var i = 0; i < scopes.Length; i++)
        {
            var track = scopes[i].Track;
            offsets[track] = Math.Max(offsets.GetValueOrDefault(track), depths[i] + 1);
        }
        rows = 0;
        foreach (var track in offsets.Keys.Order())
        {
            var height = offsets[track];
            offsets[track] = rows;
            rows += height;
        }
        return offsets;
    }

    static string ScopeText(FrameRecord frame, FlameScope scope)
    {
        var ticks = scope.Event.End - scope.Event.Start;
        var share = 100d * ticks / Math.Max(1, frame.End - frame.Start);
        return string.Create(CultureInfo.InvariantCulture,
            $"{scope.Event.Name} · track {scope.Event.Track} · {frame.ToMilliseconds(ticks):F3} ms total · {frame.ToMilliseconds(scope.Self):F3} ms self · {share:F1}% of frame #{frame.Frame}");
    }

    void DrawScope(FrameRecord frame, Rect rect, FlameScope scope, bool hovered)
    {
        if (hovered)
        {
            Gui.DrawRectBorder(rect, StyleColor("profiler-selection", "border-color"), 1.5f);
        }
        if (rect.W <= 6)
        {
            return;
        }
        var value = frame.ToMilliseconds(scope.Event.End - scope.Event.Start);
        Span<char> label = stackalloc char[256];
        var length = FormatScopeLabel(scope.Event.Name, value, label);
        if (length == 0 || !DrawScopeLabel(rect, label[..length]))
        {
            DrawScopeLabel(rect, scope.Event.Name);
        }
    }

    static int FormatScopeLabel(string name, double milliseconds, Span<char> buffer)
    {
        if (name.Length >= buffer.Length - 4 ||
            !milliseconds.TryFormat(buffer[(name.Length + 1)..^3], out var written, "F2", CultureInfo.InvariantCulture))
        {
            return 0;
        }
        name.AsSpan().CopyTo(buffer);
        buffer[name.Length] = ' ';
        " ms".CopyTo(buffer[(name.Length + written + 1)..]);
        return name.Length + written + 4;
    }

    bool DrawScopeLabel(Rect rect, ReadOnlySpan<char> text)
    {
        var layout = Gui.LayoutPlainText(text, size: 11);
        if (layout.Width + 6 > rect.W)
        {
            return false;
        }
        Gui.DrawTextLayout(layout, new Vector2(rect.X + 3, rect.Y + (rect.H - layout.Height) / 2),
            StyleColor("profiler-scope-label", "color"));
        return true;
    }

    /// <summary>Millisecond grid with the smallest round step that keeps labels at least 70 pixels apart.</summary>
    void DrawRuler(FrameRecord frame, Rect area)
    {
        var total = Math.Max(frame.Milliseconds, 1e-9);
        Span<double> ticks = stackalloc double[32];
        var count = CartesianTransforms.GenerateTicks(new ChartRange(0, total),
            1 + Math.Clamp((int)(area.W / 70), 1, ticks.Length - 1), ticks);
        Span<char> label = stackalloc char[64];
        var color = StyleColor("profiler-budget", "border-color");
        foreach (var tick in ticks[..count])
        {
            var x = area.X + (float)(tick / total) * area.W;
            Gui.DrawLine(new Vector2(x, area.Y), new Vector2(x, area.Y + area.H), color, .5f);
            if (tick.TryFormat(label[..^3], out var written, "0.###", CultureInfo.InvariantCulture))
            {
                " ms".CopyTo(label[written..]);
                ChartLabels.Draw(Gui, new Rect(x + 3, area.Y + 1, area.X + area.W - x - 3, RulerHeight),
                    label[..(written + 3)], color, size: 10);
            }
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

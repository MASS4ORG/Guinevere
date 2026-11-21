using System.Numerics;
using System.Runtime.InteropServices;
using Guinevere.Charts;

namespace Guinevere.DevTools;

public sealed partial class ProfilerView
{
    const float FlameMinimumWidth = 1;
    const float FlameHorizontalGap = 1;
    const float FlameRowGap = 2;
    ReadOnlyMemory<FlameInterval>[][] _flameLanes = [];

    void PrepareFlameLanes()
    {
        var grouped = new List<FlameInterval>?[_flameRows];
        for (var i = 0; i < _flameIntervals.Length; i++)
        {
            var span = _flameIntervals[i];
            if (span.End >= span.Start)
            {
                var end = span.End > span.Start ? span.End : Math.BitIncrement(span.Start);
                (grouped[span.Row] ??= []).Add(new FlameInterval(span.Start, end, i));
            }
        }
        _flameLanes = new ReadOnlyMemory<FlameInterval>[_flameRows][];
        for (var row = 0; row < grouped.Length; row++)
        {
            _flameLanes[row] = grouped[row] is { } intervals ? PrepareLanes(intervals) : [];
        }
    }

    static ReadOnlyMemory<FlameInterval>[] PrepareLanes(List<FlameInterval> intervals)
    {
        intervals.Sort(static (a, b) =>
        {
            var compare = a.Start.CompareTo(b.Start);
            return compare != 0 ? compare : a.SourceIndex.CompareTo(b.SourceIndex);
        });
        if (FlameRowIndex.IsValid(CollectionsMarshal.AsSpan(intervals)))
        {
            return [intervals.ToArray()];
        }
        // Interleaved events at the same depth share a visual row and use separate disjoint search lanes.
        var lanes = new List<List<FlameInterval>>();
        var available = new PriorityQueue<int, double>();
        foreach (var interval in intervals)
        {
            int lane;
            if (available.TryPeek(out _, out var end) && end <= interval.Start)
            {
                lane = available.Dequeue();
            }
            else
            {
                lane = lanes.Count;
                lanes.Add([]);
            }
            lanes[lane].Add(interval);
            available.Enqueue(lane, interval.End);
        }
        var result = new ReadOnlyMemory<FlameInterval>[lanes.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = lanes[i].ToArray();
        }
        return result;
    }

    void DrawVisibleFlame(FrameRecord frame, TimelinePlot plot, int hovered)
    {
        Span<Color> colors = stackalloc Color[SeriesSelectors.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = StyleColor(SeriesSelectors[i], "background-color");
        }
        var last = Math.Min(_flameRows, _firstFlameRow + VisibleFlameRows);
        for (var row = _firstFlameRow; row < last; row++)
        {
            foreach (var lane in _flameLanes[row])
            {
                foreach (var interval in Visible(lane.Span, plot.Time))
                {
                    var index = interval.SourceIndex;
                    var scope = _flame[index];
                    var span = _flameIntervals[index] with { Row = row - _firstFlameRow, Color = colors[scope.Series] };
                    if (TimelineChart.DrawInterval(Gui, plot, span, out var bounds, FlameMinimumWidth,
                        FlameHorizontalGap, FlameRowGap, radius: 2))
                    {
                        DrawScope(frame, bounds, scope, index == hovered || index == _flameSelected);
                    }
                }
            }
        }
    }

    int HitFlame(TimelinePlot plot, Vector2 pointer)
    {
        if (!ContainsFlame(plot.Area, pointer))
        {
            return -1;
        }
        var slot = (int)((pointer.Y - plot.Area.Y) / plot.RowHeight);
        var row = slot + _firstFlameRow;
        if (slot >= VisibleFlameRows || row >= _flameRows)
        {
            return -1;
        }
        var x = (double)pointer.X - plot.Area.X;
        var min = plot.Time.Denormalize(Math.Max(0, x - FlameMinimumWidth) / plot.Area.W);
        var max = plot.Time.Denormalize(Math.Min(plot.Area.W, x + 1) / plot.Area.W);
        var window = new ChartRange(min, max);
        var lanes = _flameLanes[row];
        for (var lane = lanes.Length - 1; lane >= 0; lane--)
        {
            var visible = Visible(lanes[lane].Span, window.IsValid ? window : plot.Time);
            for (var i = visible.Length - 1; i >= 0; i--)
            {
                var index = visible[i].SourceIndex;
                var span = _flameIntervals[index];
                if (plot.TryProject(span.Start, span.End, slot, out var bounds,
                    FlameMinimumWidth, FlameHorizontalGap, FlameRowGap) &&
                    ContainsFlame(bounds, pointer))
                {
                    return index;
                }
            }
        }
        return -1;
    }

    static ReadOnlySpan<FlameInterval> Visible(ReadOnlySpan<FlameInterval> intervals, ChartRange time) =>
        intervals[FlameRowIndex.VisibleRange(intervals, time.Min, time.Max)];

    static bool ContainsFlame(Rect rect, Vector2 pointer) => pointer.X >= rect.X && pointer.Y >= rect.Y &&
        pointer.X < rect.X + rect.W && pointer.Y < rect.Y + rect.H;
}

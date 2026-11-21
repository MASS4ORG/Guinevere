using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Creates, draws and hit-tests pie and donut slices.</summary>
public static class PieChart
{
    /// <summary>Writes slices in input order and returns the required count, which may exceed destination length.</summary>
    /// <remarks>Only finite, positive weights with a visible sweep are emitted. Angles and gaps are clockwise radians.</remarks>
    public static int CreateSegments(ReadOnlySpan<double> weights, Span<PieSegment> destination,
        double startAngle = 0, double angularGap = 0)
    {
        var source = new SliceReader(weights, startAngle, angularGap);
        var count = 0;
        while (source.TryRead(out var segment))
        {
            if (count < destination.Length)
            {
                destination[count] = segment;
            }
            count++;
        }
        return count;
    }

    /// <summary>Returns the source index for a point in y-down unit-circle coordinates, or -1.</summary>
    /// <remarks>The hole radius is in [0, 1). For repeated hits, use the overload accepting prepared slices.</remarks>
    public static int HitTest(ReadOnlySpan<double> weights, double x, double y, double holeRadius = 0,
        double startAngle = 0, double angularGap = 0)
    {
        CheckHole(holeRadius);
        if (!TryAngle(x, y, holeRadius, out var angle))
        {
            return -1;
        }
        var source = new SliceReader(weights, startAngle, angularGap);
        while (source.TryRead(out var segment))
        {
            if (ContainsAngle(segment, angle))
            {
                return segment.SourceIndex;
            }
        }
        return -1;
    }

    /// <summary>Returns the source index of a prepared slice under a screen-space pointer, or -1.</summary>
    public static int HitTest(Rect area, ReadOnlySpan<PieSegment> segments, Vector2 pointer, double holeRadius = 0,
        PieSelection? selection = null)
    {
        CheckHole(holeRadius);
        if (!ChartArea.IsValid(area))
        {
            return -1;
        }
        var radius = Math.Min(area.W, area.H) / 2d;
        var center = area.Center;
        var delta = pointer - center;
        _ = TryAngle(delta.X / radius, delta.Y / radius, holeRadius, out var baseAngle);
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var segment = segments[i];
            var angle = baseAngle;
            if (selection is { } selected && selected.SourceIndex == segment.SourceIndex)
            {
                var moved = pointer - CenterFor(center, segment, selection);
                _ = TryAngle(moved.X / radius, moved.Y / radius, holeRadius, out angle);
            }
            if (ContainsAngle(segment, angle))
            {
                return segment.SourceIndex;
            }
        }
        return -1;
    }

    /// <summary>Draws prepared slices using a cyclic palette indexed by the original weights.</summary>
    public static void Draw(Gui gui, Rect area, ReadOnlySpan<PieSegment> segments, ReadOnlySpan<Color> palette,
        float holeRadius = 0, PieSelection? selection = null)
    {
        CheckHole(holeRadius);
        if (gui.Pass != Pass.Pass2Render || !ChartArea.IsValid(area) || palette.IsEmpty)
        {
            return;
        }
        var radius = Math.Min(area.W, area.H) / 2;
        var center = area.Center;
        foreach (var segment in segments)
        {
            if (segment.SourceIndex >= 0)
            {
                var start = (float)(segment.StartAngle * (180 / Math.PI));
                var sweep = (float)(segment.SweepAngle * (180 / Math.PI));
                var color = selection is { Color: { } selectedColor } selected && selected.SourceIndex == segment.SourceIndex
                    ? selectedColor : palette[segment.SourceIndex % palette.Length];
                gui.DrawRingSector(CenterFor(center, segment, selection), radius, radius * holeRadius, start, sweep, color);
            }
        }
    }

    static Vector2 CenterFor(Vector2 center, PieSegment segment, PieSelection? selection)
    {
        if (selection is not { } selected || selected.SourceIndex != segment.SourceIndex ||
            !float.IsFinite(selected.Offset) || selected.Offset <= 0)
        {
            return center;
        }
        var (sin, cos) = Math.SinCos(segment.StartAngle + segment.SweepAngle / 2);
        return center + new Vector2((float)cos, (float)sin) * selected.Offset;
    }

    static bool TryAngle(double x, double y, double holeRadius, out double angle)
    {
        var distance = x * x + y * y;
        angle = distance >= holeRadius * holeRadius && distance <= 1 ? Math.Atan2(y, x) : double.NaN;
        return double.IsFinite(angle);
    }

    static void CheckHole(double holeRadius)
    {
        if (holeRadius is not (>= 0 and < 1))
        {
            throw new ArgumentOutOfRangeException(nameof(holeRadius));
        }
    }

    static bool ContainsAngle(PieSegment segment, double angle) =>
        Wrap(angle - segment.StartAngle) < segment.SweepAngle;

    static double Wrap(double angle)
    {
        var wrapped = angle % Math.Tau;
        return wrapped < 0 ? wrapped + Math.Tau : wrapped;
    }

    ref struct SliceReader
    {
        readonly ReadOnlySpan<double> _weights;
        readonly double _max;
        readonly double _total;
        readonly double _start;
        readonly double _gap;
        int _index;
        double _cumulative;

        internal SliceReader(ReadOnlySpan<double> weights, double startAngle, double gap)
        {
            if (!double.IsFinite(startAngle))
            {
                throw new ArgumentOutOfRangeException(nameof(startAngle));
            }
            if (!double.IsFinite(gap) || gap < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gap));
            }
            _weights = weights;
            _start = Wrap(startAngle);
            _gap = gap;
            foreach (var weight in weights)
            {
                if (double.IsFinite(weight) && weight > _max)
                {
                    _max = weight;
                }
            }
            if (_max > 0)
            {
                foreach (var weight in weights)
                {
                    if (double.IsFinite(weight) && weight > 0)
                    {
                        _total += weight / _max;
                    }
                }
            }
        }

        internal bool TryRead(out PieSegment segment)
        {
            while (_max > 0 && _index < _weights.Length)
            {
                var index = _index++;
                var weight = _weights[index];
                if (!double.IsFinite(weight) || weight <= 0)
                {
                    continue;
                }
                var from = _start + Math.Tau * (_cumulative / _total);
                _cumulative += weight / _max;
                var to = _start + Math.Tau * (_cumulative / _total);
                var sweep = to - from - _gap;
                if (sweep > 0)
                {
                    segment = new PieSegment(index, Wrap(from + _gap / 2), sweep);
                    return true;
                }
            }
            segment = default;
            return false;
        }
    }
}

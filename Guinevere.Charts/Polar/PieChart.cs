using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Creates, draws and hit-tests pie and donut slices.</summary>
public static class PieChart
{
    /// <summary>Writes slices in input order and returns the required count, which may exceed destination length.</summary>
    /// <remarks>Only finite, positive weights with a visible sweep are emitted. Empty and all-zero data produce zero slices. All angles are in clockwise radians.</remarks>
    public static int CreateSegments(ReadOnlySpan<double> weights, Span<PieSegment> destination,
        double startAngle = 0, double angularGap = 0, double angularSweep = Math.Tau)
    {
        var source = new SliceReader(weights, startAngle, angularGap, angularSweep);
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
        double startAngle = 0, double angularGap = 0, double angularSweep = Math.Tau)
    {
        CheckRadii(holeRadius, 1);
        if (!TryAngle(x, y, holeRadius, 1, out var angle))
        {
            return -1;
        }
        var source = new SliceReader(weights, startAngle, angularGap, angularSweep);
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
    /// <remarks>Both radii are fractions of the full chart radius. Hit-test concentric rings in reverse draw order.</remarks>
    public static int HitTest(Rect area, ReadOnlySpan<PieSegment> segments, Vector2 pointer, double holeRadius = 0,
        PieSelection? selection = null, double outerRadius = 1)
    {
        CheckRadii(holeRadius, outerRadius);
        if (!ChartArea.IsValid(area))
        {
            return -1;
        }
        var radius = Math.Min(area.W, area.H) / 2d;
        var center = area.Center;
        var delta = pointer - center;
        _ = TryAngle(delta.X / radius, delta.Y / radius, holeRadius, outerRadius, out var baseAngle);
        for (var i = segments.Length - 1; i >= 0; i--)
        {
            var segment = segments[i];
            var angle = baseAngle;
            if (selection is { } selected && selected.SourceIndex == segment.SourceIndex)
            {
                var moved = pointer - CenterFor(center, segment, selection);
                _ = TryAngle(moved.X / radius, moved.Y / radius, holeRadius, outerRadius, out angle);
            }
            if (ContainsAngle(segment, angle))
            {
                return segment.SourceIndex;
            }
        }
        return -1;
    }

    /// <summary>Draws prepared slices using a cyclic palette indexed by the original weights. Empty segments leave the area blank.</summary>
    /// <remarks>Both radii are fractions of the full chart radius. Draw multiple rings by providing separate segments and radii.</remarks>
    public static void Draw(Gui gui, Rect area, ReadOnlySpan<PieSegment> segments, ReadOnlySpan<Color> palette,
        float holeRadius = 0, PieSelection? selection = null, float outerRadius = 1)
    {
        CheckRadii(holeRadius, outerRadius);
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
                gui.DrawRingSector(CenterFor(center, segment, selection), radius * outerRadius, radius * holeRadius,
                    start, sweep, color);
            }
        }
    }

    /// <summary>Draws source labels that fit inside their slices and returns the number drawn.</summary>
    /// <remarks>Use the same radii and selection as for drawing. Use a legend to show labels omitted for space.</remarks>
    public static int DrawLabels(Gui gui, Rect area, ReadOnlySpan<PieSegment> segments, ReadOnlySpan<string> labels,
        Color color, float holeRadius = 0, PieSelection? selection = null, float outerRadius = 1,
        float size = 0, Font? font = null)
    {
        CheckRadii(holeRadius, outerRadius);
        if (gui.Pass != Pass.Pass2Render || !ChartArea.IsValid(area))
        {
            return 0;
        }
        var radius = Math.Min(area.W, area.H) / 2;
        var mid = (holeRadius + outerRadius) / 2 * radius;
        var padding = (outerRadius - holeRadius) / 2 * radius;
        var count = 0;
        foreach (var segment in segments)
        {
            if ((uint)segment.SourceIndex >= (uint)labels.Length || string.IsNullOrEmpty(labels[segment.SourceIndex]) ||
                segment.SweepAngle is not (> 0 and <= Math.Tau) || !double.IsFinite(segment.StartAngle))
            {
                continue;
            }
            var layout = gui.LayoutPlainText(labels[segment.SourceIndex], size: size, font: font);
            var halfSize = new Vector2(layout.Width / 2, layout.Height / 2);
            var room = segment.SweepAngle >= Math.PI ? padding
                : Math.Min(padding, mid * Math.Sin(segment.SweepAngle / 2));
            if (halfSize.Length() + 2 > room)
            {
                continue;
            }
            var (sin, cos) = Math.SinCos(segment.StartAngle + segment.SweepAngle / 2);
            var point = CenterFor(area.Center, segment, selection) + new Vector2((float)cos, (float)sin) * mid;
            var origin = point - halfSize;
            if (origin.X >= area.X && origin.Y >= area.Y &&
                point.X + halfSize.X <= area.X + area.W && point.Y + halfSize.Y <= area.Y + area.H)
            {
                gui.DrawTextLayout(layout, origin, color);
                count++;
            }
        }
        return count;
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

    static bool TryAngle(double x, double y, double holeRadius, double outerRadius, out double angle)
    {
        var distance = x * x + y * y;
        angle = distance >= holeRadius * holeRadius && distance <= outerRadius * outerRadius
            ? Math.Atan2(y, x) : double.NaN;
        return double.IsFinite(angle);
    }

    static void CheckRadii(double holeRadius, double outerRadius)
    {
        if (outerRadius is not (> 0 and <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(outerRadius));
        }
        if (!(holeRadius >= 0 && holeRadius < outerRadius))
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
        readonly double _sweep;
        int _index;
        double _cumulative;

        internal SliceReader(ReadOnlySpan<double> weights, double startAngle, double gap, double angularSweep)
        {
            if (!double.IsFinite(startAngle))
            {
                throw new ArgumentOutOfRangeException(nameof(startAngle));
            }
            if (!double.IsFinite(gap) || gap < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gap));
            }
            if (angularSweep is not (> 0 and <= Math.Tau))
            {
                throw new ArgumentOutOfRangeException(nameof(angularSweep));
            }
            _weights = weights;
            _start = Wrap(startAngle);
            _gap = gap;
            _sweep = angularSweep;
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
                var from = _start + _sweep * (_cumulative / _total);
                _cumulative += weight / _max;
                var to = _start + _sweep * (_cumulative / _total);
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

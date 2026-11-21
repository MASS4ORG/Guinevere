using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Projects independently ranged values onto a regular radar chart in y-down screen coordinates.</summary>
public static class RadarProjection
{
    /// <summary>Returns the unit-circle vertex for an axis, with axis zero at <paramref name="startAngleRadians"/>.</summary>
    public static Vector2 AxisVertex(int axisIndex, int dimensionCount, float startAngleRadians = -MathF.PI / 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensionCount, 3);
        ArgumentOutOfRangeException.ThrowIfNegative(axisIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(axisIndex, dimensionCount);
        ValidateAngle(startAngleRadians);
        return Direction(startAngleRadians % Math.Tau + axisIndex * (Math.Tau / dimensionCount));
    }

    /// <summary>Writes one unit vertex per axis in dimension order for a chart with at least three dimensions.</summary>
    public static void CreateAxes(Span<Vector2> axes, float startAngleRadians = -MathF.PI / 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(axes.Length, 3);
        ValidateAngle(startAngleRadians);
        var start = startAngleRadians % Math.Tau;
        var step = Math.Tau / axes.Length;
        for (var i = 0; i < axes.Length; i++)
        {
            axes[i] = Direction(start + i * step);
        }
    }

    /// <summary>Projects one series into caller-owned storage. Invalid axes or values produce NaN vertices.</summary>
    public static bool Project(ReadOnlySpan<ChartRange> ranges, ReadOnlySpan<double> values, Span<Vector2> output,
        float startAngleRadians = -MathF.PI / 2)
    {
        if (ranges.Length < 3)
        {
            throw new ArgumentException("Radar charts require at least three dimensions.", nameof(ranges));
        }
        if (values.Length != ranges.Length)
        {
            throw new ArgumentException("Each radar axis requires one value.", nameof(values));
        }
        if (output.Length < ranges.Length)
        {
            throw new ArgumentException("Output must fit every radar vertex.", nameof(output));
        }

        CreateAxes(output[..ranges.Length], startAngleRadians);
        var complete = true;
        for (var i = 0; i < ranges.Length; i++)
        {
            var range = ranges[i];
            var value = values[i];
            if (!range.IsValid || !double.IsFinite(value))
            {
                output[i] = new Vector2(float.NaN, float.NaN);
                complete = false;
                continue;
            }

            var normalized = Math.Clamp(range.Normalize(value), 0d, 1d);
            output[i] *= (float)normalized;
        }
        return complete;
    }

    /// <summary>Finds the nearest axis endpoint in unit-circle space, or -1 outside the supplied tolerance.</summary>
    public static int HitTestAxis(Vector2 point, int dimensionCount, float tolerance,
        float startAngleRadians = -MathF.PI / 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensionCount, 3);
        ValidateAngle(startAngleRadians);
        ValidateTolerance(tolerance);
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            return -1;
        }
        var start = startAngleRadians % Math.Tau;
        var step = Math.Tau / dimensionCount;
        var best = (double)tolerance * tolerance;
        var result = -1;
        for (var i = 0; i < dimensionCount; i++)
        {
            var distance = DistanceSquared(point, Direction(start + i * step));
            if (distance <= best)
            {
                best = distance;
                result = i;
            }
        }
        return result;
    }

    /// <summary>Finds the nearest finite projected vertex index, or -1 outside the supplied tolerance.</summary>
    public static int HitTestVertex(Vector2 point, ReadOnlySpan<Vector2> vertices, float tolerance)
    {
        ValidateTolerance(tolerance);
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            return -1;
        }
        var best = (double)tolerance * tolerance;
        var result = -1;
        for (var i = 0; i < vertices.Length; i++)
        {
            var distance = DistanceSquared(point, vertices[i]);
            if (distance <= best)
            {
                best = distance;
                result = i;
            }
        }
        return result;
    }

    static Vector2 Direction(double angle)
    {
        var (sin, cos) = Math.SinCos(angle);
        return new Vector2((float)cos, (float)sin);
    }

    static double DistanceSquared(Vector2 point, Vector2 vertex)
    {
        var x = (double)point.X - vertex.X;
        var y = (double)point.Y - vertex.Y;
        return x * x + y * y;
    }

    static void ValidateAngle(float startAngleRadians)
    {
        if (!float.IsFinite(startAngleRadians))
        {
            throw new ArgumentOutOfRangeException(nameof(startAngleRadians));
        }
    }

    static void ValidateTolerance(float tolerance)
    {
        if (!float.IsFinite(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }
    }
}

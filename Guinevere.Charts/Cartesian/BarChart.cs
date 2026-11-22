using System.Numerics;

namespace Guinevere.Charts;

/// <summary>A visible bar and its indices in the caller's category and series arrays.</summary>
/// <param name="Bounds">Clipped screen bounds.</param>
/// <param name="Category">Source category index.</param>
/// <param name="Series">Source series index.</param>
public readonly record struct BarChartRect(Rect Bounds, int Category, int Series);

/// <summary>Projects, draws and hit tests grouped and stacked Cartesian bars.</summary>
public static class BarChart
{
    /// <summary>Projects category-major values into caller-owned storage, returning the visible rectangle count.</summary>
    /// <remarks>
    /// Values are offsets from baseline, ordered by category then series. Stack indices must start at zero and
    /// increase contiguously in series order; distinct indices form side-by-side groups. The destination must hold
    /// all values. Invalid configuration returns zero without writing; nonfinite values and overflowing sums are
    /// skipped. Positive and negative values accumulate independently in each stack. Width is in screen pixels.
    /// </remarks>
    public static int Project(CartesianPlot plot, ReadOnlySpan<double> categories, ReadOnlySpan<double> values,
        ReadOnlySpan<int> stacks, Span<BarChartRect> destination, float width, double baseline = 0,
        bool horizontal = false)
    {
        if (!plot.IsValid || stacks.IsEmpty || (long)categories.Length * stacks.Length != values.Length ||
            destination.Length < values.Length || !float.IsFinite(width) || width <= 0 || !double.IsFinite(baseline) ||
            !AreValidStacks(stacks))
        {
            return 0;
        }
        var groupWidth = (double)width / (stacks[^1] + 1);
        var categoryRange = horizontal ? plot.Y : plot.X;
        var valueRange = horizontal ? plot.X : plot.Y;
        var count = 0;
        for (var c = 0; c < categories.Length; c++)
        {
            var position = categoryRange.Normalize(categories[c]);
            if (!CartesianPlot.IsUnit(position))
            {
                continue;
            }
            var center = horizontal ? plot.Area.Y + (1 - position) * plot.Area.H : plot.Area.X + position * plot.Area.W;
            count += ProjectCategory(plot.Area, valueRange, c, values.Slice(c * stacks.Length, stacks.Length), stacks,
                destination[count..], center - width / 2d, groupWidth, baseline, horizontal);
        }
        return count;
    }

    static bool AreValidStacks(ReadOnlySpan<int> stacks)
    {
        if (stacks[0] != 0)
        {
            return false;
        }
        for (var i = 1; i < stacks.Length; i++)
        {
            if ((long)stacks[i] - stacks[i - 1] is not (0 or 1))
            {
                return false;
            }
        }
        return true;
    }

    static int ProjectCategory(Rect area, ChartRange range, int category, ReadOnlySpan<double> values,
        ReadOnlySpan<int> stacks, Span<BarChartRect> destination, double first, double width, double baseline,
        bool horizontal)
    {
        var positive = baseline;
        var negative = baseline;
        var group = -1;
        var count = 0;
        for (var series = 0; series < stacks.Length; series++)
        {
            if (stacks[series] != group)
            {
                group = stacks[series];
                positive = baseline;
                negative = baseline;
            }
            var value = values[series];
            ref var total = ref (value >= 0 ? ref positive : ref negative);
            var end = total + value;
            if (!double.IsFinite(end))
            {
                continue;
            }
            var a = range.Normalize(Math.Clamp(total, range.Min, range.Max));
            var b = range.Normalize(Math.Clamp(end, range.Min, range.Max));
            total = end;
            var leading = first + group * width;
            var rect = BarBounds(area, leading, leading + width, Math.Min(a, b), Math.Max(a, b), horizontal);
            if (rect.W > 0 && rect.H > 0)
            {
                destination[count++] = new BarChartRect(rect, category, series);
            }
        }
        return count;
    }

    static Rect BarBounds(Rect area, double leading, double trailing, double low, double high, bool horizontal)
    {
        if (horizontal)
        {
            var left = area.X + low * area.W;
            var right = area.X + high * area.W;
            var top = Math.Max(leading, area.Y);
            var bottom = Math.Min(trailing, (double)area.Y + area.H);
            return new Rect((float)left, (float)top, (float)(right - left), (float)(bottom - top));
        }
        var x = Math.Max(leading, area.X);
        var end = Math.Min(trailing, (double)area.X + area.W);
        var y = area.Y + (1 - high) * area.H;
        var bottomEdge = area.Y + (1 - low) * area.H;
        return new Rect((float)x, (float)y, (float)(end - x), (float)(bottomEdge - y));
    }

    /// <summary>Draws prepared rectangles using colors indexed by source series; missing colors are skipped.</summary>
    public static void Draw(Gui gui, ReadOnlySpan<BarChartRect> bars, ReadOnlySpan<Color> colors)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        foreach (var bar in bars)
        {
            if ((uint)bar.Series < (uint)colors.Length)
            {
                gui.DrawRect(bar.Bounds, colors[bar.Series]);
            }
        }
    }

    /// <summary>Returns the index of the topmost prepared rectangle containing the pointer, or -1.</summary>
    public static int HitTest(ReadOnlySpan<BarChartRect> bars, Vector2 pointer)
    {
        for (var i = bars.Length - 1; i >= 0; i--)
        {
            if (ChartArea.Contains(bars[i].Bounds, pointer))
            {
                return i;
            }
        }
        return -1;
    }
}

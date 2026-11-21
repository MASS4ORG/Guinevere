using System.Globalization;
using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Controls which Cartesian axis a set of labels describes.</summary>
public enum ChartAxis
{
    /// <summary>The horizontal data range.</summary>
    X,
    /// <summary>The vertical data range.</summary>
    Y
}

/// <summary>Draws chart labels in caller-supplied screen-space bounds.</summary>
public static class ChartLabels
{
    /// <summary>Draws an unwrapped label at the top-left when its entire layout fits.</summary>
    public static bool Draw(Gui gui, Rect bounds, ReadOnlySpan<char> text, Color color, float size = 0, Font? font = null)
    {
        if (gui.Pass != Pass.Pass2Render || !ChartArea.IsValid(bounds) || text.IsEmpty)
        {
            return false;
        }
        var layout = gui.LayoutPlainText(text, size: size, font: font);
        if (layout.Width > bounds.W || layout.Height > bounds.H)
        {
            return false;
        }
        gui.DrawTextLayout(layout, bounds.Position, color);
        return true;
    }

    /// <summary>Draws a tooltip near its anchor, clamped to the available bounds, or omits it if it cannot fit.</summary>
    public static bool DrawTooltip(Gui gui, Rect bounds, Vector2 anchor, ReadOnlySpan<char> text, ChartStyle style,
        float size = 0, Font? font = null)
    {
        if (gui.Pass != Pass.Pass2Render || !ChartArea.IsValid(bounds) || !ChartArea.Contains(bounds, anchor) ||
            text.IsEmpty)
        {
            return false;
        }
        const float Padding = 4;
        var layout = gui.LayoutPlainText(text, size: size, font: font);
        var width = layout.Width + Padding * 2;
        var height = layout.Height + Padding * 2;
        if (width > bounds.W || height > bounds.H)
        {
            return false;
        }
        var x = Math.Min(anchor.X + 12, bounds.X + (bounds.W - width));
        var y = Math.Min(anchor.Y + 12, bounds.Y + (bounds.H - height));
        gui.DrawRect(new Rect(x, y, width, height), style.TooltipBackground, radius: Padding);
        gui.DrawTextLayout(layout, new Vector2(x + Padding, y + Padding), style.TooltipText);
        return true;
    }

    /// <summary>Draws ascending ticks in axis order, omitting labels that overlap or extend outside the supplied bounds.</summary>
    public static int DrawTicks(Gui gui, CartesianPlot plot, Rect bounds, ChartAxis axis, ReadOnlySpan<double> ticks,
        Color color, float size = 0, Font? font = null, ReadOnlySpan<char> format = default)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid || !ChartArea.IsValid(bounds))
        {
            return 0;
        }
        var horizontal = axis == ChartAxis.X;
        var range = horizontal ? plot.X : plot.Y;
        var occupied = float.NegativeInfinity;
        var drawn = 0;
        Span<char> text = stackalloc char[64];
        for (var i = 0; i < ticks.Length; i++)
        {
            var tick = horizontal ? ticks[i] : ticks[^(i + 1)];
            var normalized = range.Normalize(tick);
            if (!CartesianPlot.IsUnit(normalized) ||
                !tick.TryFormat(text, out var written, format.IsEmpty ? "G6" : format, CultureInfo.InvariantCulture))
            {
                continue;
            }
            var layout = gui.LayoutPlainText(text[..written], size: size, font: font);
            var location = horizontal
                ? new Vector2(plot.Project(normalized, 0).X - layout.Width / 2, bounds.Y)
                : new Vector2(bounds.X + bounds.W - layout.Width, plot.Project(0, normalized).Y - layout.Height / 2);
            var leading = horizontal ? location.X : location.Y;
            if (leading < occupied || !Fits(bounds, location, layout))
            {
                continue;
            }
            gui.DrawTextLayout(layout, location, color);
            occupied = leading + (horizontal ? layout.Width : layout.Height) + 4;
            drawn++;
        }
        return drawn;
    }

    static bool Fits(Rect bounds, Vector2 location, TextLayout layout) =>
        location.X >= bounds.X && location.Y >= bounds.Y &&
        location.X + layout.Width <= bounds.X + bounds.W && location.Y + layout.Height <= bounds.Y + bounds.H;
}

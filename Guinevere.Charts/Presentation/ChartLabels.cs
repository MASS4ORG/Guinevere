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

/// <summary>Places a scale on one edge of a Cartesian plot.</summary>
public enum ChartAxisSide
{
    /// <summary>The horizontal scale above the plot.</summary>
    Top,
    /// <summary>The horizontal scale below the plot.</summary>
    Bottom,
    /// <summary>The vertical scale to the left of the plot.</summary>
    Left,
    /// <summary>The vertical scale to the right of the plot.</summary>
    Right
}

/// <summary>Places a label along one dimension of its available bounds.</summary>
public enum ChartLabelAlignment
{
    /// <summary>Aligns to the left or top.</summary>
    Start,
    /// <summary>Centers the label.</summary>
    Center,
    /// <summary>Aligns to the right or bottom.</summary>
    End
}

/// <summary>Draws chart labels in caller-supplied screen-space bounds.</summary>
public static class ChartLabels
{
    /// <summary>Draws an aligned, unwrapped label when its entire layout fits. Use the same method for titles.</summary>
    public static bool Draw(Gui gui, Rect bounds, ReadOnlySpan<char> text, Color color, float size = 0,
        Font? font = null, ChartLabelAlignment horizontal = ChartLabelAlignment.Start,
        ChartLabelAlignment vertical = ChartLabelAlignment.Start)
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
        var origin = bounds.Position + new Vector2(Align(bounds.W - layout.Width, horizontal),
            Align(bounds.H - layout.Height, vertical));
        gui.DrawTextLayout(layout, origin, color);
        return true;
    }

    static float Align(float room, ChartLabelAlignment alignment) => alignment switch
    {
        ChartLabelAlignment.Center => room / 2,
        ChartLabelAlignment.End => room,
        _ => 0,
    };

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
        => DrawTicks(gui, plot, bounds, axis == ChartAxis.X ? ChartAxisSide.Bottom : ChartAxisSide.Left,
            ticks, color, size, font, format);

    /// <summary>Draws ascending tick labels for the specified scale edge, omitting overlaps and labels outside bounds.</summary>
    public static int DrawTicks(Gui gui, CartesianPlot plot, Rect bounds, ChartAxisSide side, ReadOnlySpan<double> ticks,
        Color color, float size = 0, Font? font = null, ReadOnlySpan<char> format = default)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid || !ChartArea.IsValid(bounds))
        {
            return 0;
        }
        var horizontal = side is ChartAxisSide.Top or ChartAxisSide.Bottom;
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
            var location = TickLabelPosition(plot, bounds, side, normalized, layout);
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

    static Vector2 TickLabelPosition(CartesianPlot plot, Rect bounds, ChartAxisSide side, double normalized,
        TextLayout layout)
    {
        var screen = plot.Project(normalized, normalized);
        return side switch
        {
            ChartAxisSide.Top => new Vector2(screen.X - layout.Width / 2, bounds.Y + bounds.H - layout.Height),
            ChartAxisSide.Bottom => new Vector2(screen.X - layout.Width / 2, bounds.Y),
            ChartAxisSide.Right => new Vector2(bounds.X, screen.Y - layout.Height / 2),
            _ => new Vector2(bounds.X + bounds.W - layout.Width, screen.Y - layout.Height / 2),
        };
    }

    static bool Fits(Rect bounds, Vector2 location, TextLayout layout) =>
        location.X >= bounds.X && location.Y >= bounds.Y &&
        location.X + layout.Width <= bounds.X + bounds.W && location.Y + layout.Height <= bounds.Y + bounds.H;
}

using System.Numerics;

namespace Guinevere.Charts;

/// <summary>A legend entry with an optional text label and its series swatch color.</summary>
/// <param name="Label">The text shown next to the swatch.</param>
/// <param name="Color">The color of the series swatch.</param>
public readonly record struct ChartLegendEntry(string? Label, Color Color);

/// <summary>Draws caller-provided legend entries in rows.</summary>
public static class ChartLegend
{
    /// <summary>Draws entries in order and returns the count drawn. Entries too wide for a row are omitted.</summary>
    public static int Draw(Gui gui, Rect bounds, ReadOnlySpan<ChartLegendEntry> entries, Color textColor,
        float size = 0, Font? font = null, float swatchSize = 10, float gap = 5, bool wrap = true)
    {
        if (gui.Pass != Pass.Pass2Render || !ChartArea.IsValid(bounds) ||
            !float.IsFinite(swatchSize) || swatchSize <= 0 || !float.IsFinite(gap) || gap < 0)
        {
            return 0;
        }
        var right = bounds.X + bounds.W;
        var bottom = bounds.Y + bounds.H;
        var x = bounds.X;
        var y = bounds.Y;
        var rowHeight = 0f;
        var count = 0;
        foreach (var entry in entries)
        {
            var layout = gui.LayoutPlainText(entry.Label.AsSpan(), size: size, font: font);
            var width = swatchSize + gap + layout.Width;
            var height = Math.Max(swatchSize, layout.Height);
            if (width > bounds.W)
            {
                continue;
            }
            if (x + width > right)
            {
                if (!wrap)
                {
                    break;
                }
                x = bounds.X;
                y += rowHeight + gap;
                rowHeight = 0;
            }
            if (y + height > bottom)
            {
                continue;
            }
            gui.DrawRect(new Rect(x, y + (height - swatchSize) / 2, swatchSize, swatchSize), entry.Color);
            gui.DrawTextLayout(layout, new Vector2(x + swatchSize + gap, y + (height - layout.Height) / 2), textColor);
            x += width + gap;
            rowHeight = Math.Max(rowHeight, height);
            count++;
        }
        return count;
    }
}

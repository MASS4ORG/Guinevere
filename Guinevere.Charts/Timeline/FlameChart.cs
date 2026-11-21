using System.Numerics;

namespace Guinevere.Charts;

/// <summary>Draws and hit-tests a viewport into preindexed flame chart lanes.</summary>
public static class FlameChart
{
    /// <summary>Draws visible rows and intervals, coloring by source index in a cyclic palette.</summary>
    /// <remarks>Each source row must be sorted and disjoint as defined by <see cref="FlameRowIndex.IsValid"/>.</remarks>
    public static void Draw(Gui gui, TimelinePlot plot, ReadOnlySpan<ReadOnlyMemory<FlameInterval>> rows,
        ReadOnlySpan<Color> palette, int firstRow = 0, int selected = -1, Color? selectionColor = null)
    {
        if (gui.Pass != Pass.Pass2Render || !plot.IsValid || !HasRow(firstRow, rows.Length) || palette.IsEmpty)
        {
            return;
        }
        var visibleRows = (int)Math.Min(rows.Length - firstRow, Math.Ceiling((double)plot.Area.H / plot.RowHeight));
        for (var slot = 0; slot < visibleRows; slot++)
        {
            foreach (var interval in Visible(rows[firstRow + slot].Span, plot.Time))
            {
                if (!Project(plot, interval, slot, 1, out var bounds))
                {
                    continue;
                }
                gui.DrawRect(bounds, palette[interval.SourceIndex % palette.Length]);
                if (selected == interval.SourceIndex && selectionColor is { } outline)
                {
                    gui.DrawRectBorder(bounds, outline, thickness: 1);
                }
            }
        }
    }

    /// <summary>Returns the topmost visible source interval in the hovered row, or -1.</summary>
    public static int HitTest(TimelinePlot plot, ReadOnlySpan<ReadOnlyMemory<FlameInterval>> rows,
        Vector2 pointer, int firstRow = 0, float minimumWidth = 3)
    {
        if (!plot.IsValid || !ChartArea.Contains(plot.Area, pointer) || !HasRow(firstRow, rows.Length) ||
            !float.IsFinite(minimumWidth) || minimumWidth < 0)
        {
            return -1;
        }
        var slot = ((double)pointer.Y - plot.Area.Y) / plot.RowHeight;
        if (slot >= rows.Length - firstRow)
        {
            return -1;
        }
        var position = (double)pointer.X - plot.Area.X;
        var from = plot.Time.Denormalize(Math.Max(0, position - minimumWidth) / plot.Area.W);
        var to = plot.Time.Denormalize(Math.Min(plot.Area.W, position + 1) / plot.Area.W);
        var range = new ChartRange(from, to);
        var row = Visible(rows[firstRow + (int)slot].Span, range.IsValid ? range : plot.Time);
        for (var i = row.Length - 1; i >= 0; i--)
        {
            if (Project(plot, row[i], (int)slot, minimumWidth, out var bounds) && ChartArea.Contains(bounds, pointer))
            {
                return row[i].SourceIndex;
            }
        }
        return -1;
    }

    static bool HasRow(int row, int count) => (uint)row < (uint)count;

    static ReadOnlySpan<FlameInterval> Visible(ReadOnlySpan<FlameInterval> row, ChartRange time) =>
        row[FlameRowIndex.VisibleRange(row, time.Min, time.Max)];

    static bool Project(TimelinePlot plot, FlameInterval interval, int row, float minWidth, out Rect bounds) =>
        plot.TryProject(interval.Start, interval.End, row, out bounds, minWidth, horizontalGap: 1, rowGap: 2);
}

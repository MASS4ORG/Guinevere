namespace Guinevere;

/// <summary>
/// The resolved column geometry of one frame: how wide each visible column is and where it starts.
/// Header and rows read the same instance, so their cells always line up.
/// </summary>
sealed class TableLayout
{
    TableLayout(TableColumn[] columns, float[] widths, float[] offsets)
    {
        Columns = columns;
        Widths = widths;
        Offsets = offsets;
    }

    /// <summary>The visible columns in display order.</summary>
    public TableColumn[] Columns { get; }

    /// <summary>The width of each column.</summary>
    public float[] Widths { get; }

    /// <summary>The distance from the left edge to each column's start; one entry past the end holds the total.</summary>
    public float[] Offsets { get; }

    /// <summary>The width of every column together.</summary>
    public float Total => Offsets[^1];

    /// <summary>
    /// Decides every column's width. Fixed and auto columns take their size within their limits, and star
    /// columns split what is left by weight; a share outside a column's limits pins it there and the
    /// rest is shared again. When nothing is left, star columns take their minimum and the table scrolls.
    /// </summary>
    /// <param name="columns">The visible columns in display order.</param>
    /// <param name="viewportWidth">The width the columns fill.</param>
    /// <param name="autoWidths">The header-sized width of each column, read for auto columns.</param>
    public static TableLayout Resolve(TableColumn[] columns, float viewportWidth, float[] autoWidths)
    {
        var widths = new float[columns.Length];
        var open = new List<int>();
        var free = viewportWidth;

        for (var i = 0; i < columns.Length; i++)
        {
            var column = columns[i];
            switch (column.Width.Kind)
            {
                case TableLengthKind.Star:
                    open.Add(i);
                    break;
                case TableLengthKind.Auto:
                    widths[i] = Limit(column, autoWidths[i]);
                    free -= widths[i];
                    break;
                default:
                    widths[i] = Limit(column, column.Width.Value);
                    free -= widths[i];
                    break;
            }
        }

        ShareStars(columns, widths, open, free);

        var offsets = new float[columns.Length + 1];
        for (var i = 0; i < columns.Length; i++)
        {
            offsets[i + 1] = offsets[i] + widths[i];
        }

        return new TableLayout(columns, widths, offsets);
    }

    /// <summary>The columns whose cells touch the viewport, widened by <paramref name="overscan"/> on each side.</summary>
    /// <param name="scrollX">How far the body is scrolled sideways.</param>
    /// <param name="viewportWidth">The width in view.</param>
    /// <param name="overscan">Columns kept built beyond each edge.</param>
    public (int First, int Last) Window(float scrollX, float viewportWidth, int overscan)
    {
        var first = 0;
        while (first < Columns.Length - 1 && Offsets[first + 1] <= scrollX)
        {
            first++;
        }

        var last = first + 1;
        while (last < Columns.Length && Offsets[last] < scrollX + viewportWidth)
        {
            last++;
        }

        return (Math.Max(0, first - overscan), Math.Min(Columns.Length, last + overscan));
    }

    static float Limit(TableColumn column, float width) =>
        Math.Clamp(width, column.MinWidth, Math.Max(column.MinWidth, column.MaxWidth));

    /// <summary>
    /// Splits <paramref name="free"/> among the star columns. When the limits move the total, the columns
    /// that broke the limit in the dominant direction are pinned at it and the rest are shared again.
    /// </summary>
    static void ShareStars(TableColumn[] columns, float[] widths, List<int> open, float free)
    {
        while (open.Count > 0)
        {
            var weight = 0f;
            foreach (var i in open)
            {
                weight += columns[i].Width.Value;
            }

            var correction = 0f;
            foreach (var i in open)
            {
                var share = Share(columns[i], free, weight);
                correction += Limit(columns[i], share) - share;
            }

            if (correction == 0f)
            {
                break;
            }

            for (var k = open.Count - 1; k >= 0; k--)
            {
                var i = open[k];
                var share = Share(columns[i], free, weight);
                var limited = Limit(columns[i], share);
                if (correction > 0f ? limited <= share : limited >= share)
                {
                    continue;
                }

                widths[i] = limited;
                free -= limited;
                open.RemoveAt(k);
            }
        }

        var remaining = 0f;
        foreach (var i in open)
        {
            remaining += columns[i].Width.Value;
        }

        foreach (var i in open)
        {
            widths[i] = Share(columns[i], free, remaining);
        }
    }

    static float Share(TableColumn column, float free, float weight) =>
        Math.Max(0f, free) * column.Width.Value / weight;
}

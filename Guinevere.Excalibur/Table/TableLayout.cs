namespace Guinevere;

/// <summary>
/// The resolved column geometry of one frame: how wide each visible column is and where it starts.
/// Header and rows read the same instance, so their cells always line up.
/// </summary>
sealed class TableLayout
{
    TableLayout(TableColumn[] columns, TableLength[] lengths, float[] widths, float[] offsets)
    {
        Columns = columns;
        Lengths = lengths;
        Widths = widths;
        Offsets = offsets;

        LeftCount = 0;
        while (LeftCount < columns.Length && columns[LeftCount].Pin == TablePin.Left)
        {
            LeftCount++;
        }

        RightStart = columns.Length;
        while (RightStart > LeftCount && columns[RightStart - 1].Pin == TablePin.Right)
        {
            RightStart--;
        }
    }

    /// <summary>The visible columns in display order: left-pinned, then scrolling, then right-pinned.</summary>
    public TableColumn[] Columns { get; }

    /// <summary>How each column's width was decided, which a resize reads to know what kind of column it changes.</summary>
    public TableLength[] Lengths { get; }

    /// <summary>The width of each column.</summary>
    public float[] Widths { get; }

    /// <summary>The distance from the left edge to each column's start; one entry past the end holds the total.</summary>
    public float[] Offsets { get; }

    /// <summary>The width of every column together.</summary>
    public float Total => Offsets[^1];

    /// <summary>How many leading columns are pinned left; they never scroll.</summary>
    public int LeftCount { get; }

    /// <summary>The index of the first right-pinned column; the columns from here to the end are pinned right.</summary>
    public int RightStart { get; }

    /// <summary>The width of the left-pinned columns together.</summary>
    public float LeftWidth => Offsets[LeftCount];

    /// <summary>The width of the right-pinned columns together.</summary>
    public float RightWidth => Total - Offsets[RightStart];

    /// <summary>
    /// Decides every column's width. Fixed and auto columns take their size within their limits, and star
    /// columns split what is left by weight; a share outside a column's limits pins it there and the
    /// rest is shared again. When nothing is left, star columns take their minimum and the table scrolls.
    /// </summary>
    /// <param name="columns">The visible columns in display order.</param>
    /// <param name="lengths">How each column's width is decided: its definition or what the user chose.</param>
    /// <param name="viewportWidth">The width the columns fill.</param>
    /// <param name="autoWidths">The header-sized width of each column, read for auto columns.</param>
    public static TableLayout Resolve(TableColumn[] columns, TableLength[] lengths, float viewportWidth,
        float[] autoWidths)
    {
        var widths = new float[columns.Length];
        var open = new List<int>();
        var free = viewportWidth;

        for (var i = 0; i < columns.Length; i++)
        {
            var column = columns[i];
            switch (lengths[i].Kind)
            {
                case TableLengthKind.Star:
                    open.Add(i);
                    break;
                case TableLengthKind.Auto:
                    widths[i] = Limit(column, autoWidths[i]);
                    free -= widths[i];
                    break;
                default:
                    widths[i] = Limit(column, lengths[i].Value);
                    free -= widths[i];
                    break;
            }
        }

        ShareStars(columns, lengths, widths, open, free);

        var offsets = new float[columns.Length + 1];
        for (var i = 0; i < columns.Length; i++)
        {
            offsets[i + 1] = offsets[i] + widths[i];
        }

        return new TableLayout(columns, lengths, widths, offsets);
    }

    /// <summary>
    /// The scrolling columns whose cells touch the space the pinned columns leave in view, widened by
    /// <paramref name="overscan"/> on each side. Pinned columns are not in the window; they are always built.
    /// </summary>
    /// <param name="scrollX">How far the scrolling columns are scrolled sideways.</param>
    /// <param name="viewportWidth">The width the whole table shows, pinned columns included.</param>
    /// <param name="overscan">Columns kept built beyond each edge.</param>
    public (int First, int Last) Window(float scrollX, float viewportWidth, int overscan)
    {
        var origin = Offsets[LeftCount];
        var scrollWidth = viewportWidth - LeftWidth - RightWidth;
        var first = LeftCount;
        while (first < RightStart - 1 && Offsets[first + 1] - origin <= scrollX)
        {
            first++;
        }

        var last = first + 1;
        while (last < RightStart && Offsets[last] - origin < scrollX + scrollWidth)
        {
            last++;
        }

        return (Math.Max(LeftCount, first - overscan), Math.Min(RightStart, last + overscan));
    }

    static float Limit(TableColumn column, float width) =>
        Math.Clamp(width, column.MinWidth, Math.Max(column.MinWidth, column.MaxWidth));

    /// <summary>
    /// Splits <paramref name="free"/> among the star columns. When the limits move the total, the columns
    /// that broke the limit in the dominant direction are pinned at it and the rest are shared again.
    /// </summary>
    static void ShareStars(TableColumn[] columns, TableLength[] lengths, float[] widths, List<int> open,
        float free)
    {
        while (open.Count > 0)
        {
            var weight = 0f;
            foreach (var i in open)
            {
                weight += lengths[i].Value;
            }

            var correction = 0f;
            foreach (var i in open)
            {
                var share = Share(lengths[i], free, weight);
                correction += Limit(columns[i], share) - share;
            }

            if (correction == 0f)
            {
                break;
            }

            for (var k = open.Count - 1; k >= 0; k--)
            {
                var i = open[k];
                var share = Share(lengths[i], free, weight);
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
            remaining += lengths[i].Value;
        }

        foreach (var i in open)
        {
            widths[i] = Share(lengths[i], free, remaining);
        }
    }

    static float Share(TableLength length, float free, float weight) =>
        Math.Max(0f, free) * length.Value / weight;
}

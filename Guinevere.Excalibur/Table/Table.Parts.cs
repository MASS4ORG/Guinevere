namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] SortedAscending = ["sorted", "ascending"];
    static readonly string[] SortedDescending = ["sorted", "descending"];
    static readonly string[] SortedAscendingMore = ["sorted", "ascending", "sorted-more"];
    static readonly string[] SortedDescendingMore = ["sorted", "descending", "sorted-more"];
    static readonly string[] SecondaryAscending = ["sorted-secondary", "ascending"];
    static readonly string[] SecondaryDescending = ["sorted-secondary", "descending"];
    static readonly string[] MoreLabels = ["+1", "+2", "+3", "+4", "+5", "+6", "+7", "+8", "+9"];

    /// <summary>
    /// The header row. It sits outside the scrolling body, so it never leaves the top, and takes the
    /// body's horizontal offset as its own so its cells stay over their columns.
    /// </summary>
    static void TableHeader(Gui gui, TableState state, TableTheme theme, TableLayout layout,
        int firstColumn, int lastColumn)
    {
        using (gui.StyledNode("table-header", id: "table/header").Height(theme.HeaderHeight).ExpandWidth()
                   .Direction(Axis.Horizontal).Enter())
        {
            gui.SetLocalScrollOffset(new Vector2(state.FrameScrollX, 0f));
            gui.ClipContent();

            VirtualRows.SpacerX(gui, "table/header/padLeft", layout.Offsets[firstColumn], theme.HeaderHeight);
            for (var c = firstColumn; c < lastColumn; c++)
            {
                HeaderCell(gui, state, theme, layout.Columns[c], state.Cache.Columns!.HeaderIds[c], layout.Widths[c]);
            }

            VirtualRows.SpacerX(gui, "table/header/padRight", layout.Total - layout.Offsets[lastColumn],
                theme.HeaderHeight);
        }
    }

    static void HeaderCell(Gui gui, TableState state, TableTheme theme, TableColumn column, string id,
        float width)
    {
        var position = column.Sortable ? state.Sort.IndexOf(column) : -1;
        var more = state.Sort.SecondaryCount;
        var ascending = position >= 0 && state.Sort[position].Direction == TableSortDirection.Ascending;
        IReadOnlyList<string> modifiers = position switch
        {
            < 0 => [],
            0 => more > 0
                ? ascending ? SortedAscendingMore : SortedDescendingMore
                : ascending ? SortedAscending : SortedDescending,
            _ => ascending ? SecondaryAscending : SecondaryDescending
        };

        using (gui.StyledNode("table-header-cell", id: id, modifiers: modifiers)
                   .Width(width).Height(theme.HeaderHeight).Direction(Axis.Horizontal).Gap(4f)
                   .PaddingX(theme.CellPadding).ContentAlignX(AlignOf(column.Alignment)).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            if (column.Header is { } content)
            {
                content.Render(gui, new TableHeaderContext(column, theme.FontSize, width, theme.HeaderHeight, state));
            }
            else if (column.Title.Length > 0)
            {
                gui.DrawText(column.Title, theme.FontSize, centerInRect: false, clip: true);
            }

            if (position == 0)
            {
                gui.DrawText(ascending ? WidgetIcons.ChevronUp : WidgetIcons.ChevronDown, theme.FontSize * 0.7f);
                if (more > 0)
                {
                    gui.DrawText(more <= MoreLabels.Length ? MoreLabels[more - 1] : "+" + more, theme.FontSize * 0.7f);
                }
            }
        }
    }

    /// <summary>One body row: only the cells of the built columns, with spacers for the ones skipped.</summary>
    static void TableRow<TRow>(Gui gui, TableState state, ITableViewSource<TRow> source, TableColumn<TRow>[] shown,
        TableTheme theme, TableLayout layout, int row, int firstColumn, int lastColumn)
    {
        var slot = state.Cache.Slot(row);
        var item = source[row];
        using (gui.StyledNode("table-row", id: TableCache.RowId(slot), modifiers: row % 2 == 1 ? OddRow : [])
                   .Height(theme.RowHeight).Width(layout.Total).Direction(Axis.Horizontal).Enter())
        {
            VirtualRows.SpacerX(gui, TableCache.PadLeftId(slot), layout.Offsets[firstColumn], theme.RowHeight);
            for (var c = firstColumn; c < lastColumn; c++)
            {
                TableCell(gui, state, source, theme, slot, item, c, shown[c], layout.Widths[c]);
            }

            VirtualRows.SpacerX(gui, TableCache.PadRightId(slot), layout.Total - layout.Offsets[lastColumn],
                theme.RowHeight);
        }
    }

    static void TableCell<TRow>(Gui gui, TableState state, ITableViewSource<TRow> source, TableTheme theme,
        TableRowSlot slot, TRow item, int index, TableColumn<TRow> column, float width)
    {
        using (gui.StyledNode("table-cell", id: state.Cache.CellId(slot, index))
                   .Width(width).Height(theme.RowHeight).Direction(Axis.Horizontal).PaddingX(theme.CellPadding)
                   .ContentAlignX(AlignOf(column.Alignment)).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            column.RenderCell(gui, item, new TableCellFrame<TRow>(slot.Row, index, slot, state.Cache, source, state,
                theme.FontSize, width, theme.RowHeight));
        }
    }

    static float AlignOf(TableAlignment alignment) => alignment switch
    {
        TableAlignment.Center => 0.5f,
        TableAlignment.Right => 1f,
        _ => 0f
    };
}

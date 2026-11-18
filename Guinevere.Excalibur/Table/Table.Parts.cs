namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] SortedAscending = ["sorted", "ascending"];
    static readonly string[] SortedDescending = ["sorted", "descending"];

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
                HeaderCell(gui, state, theme, layout.Columns[c], state.Cache.HeaderIds[c], layout.Widths[c]);
            }

            VirtualRows.SpacerX(gui, "table/header/padRight", layout.Total - layout.Offsets[lastColumn],
                theme.HeaderHeight);
        }
    }

    static void HeaderCell(Gui gui, TableState state, TableTheme theme, TableColumn column, string id,
        float width)
    {
        var sort = state.Sort is { } active && active.ColumnId == column.Id ? active : (TableSort?)null;
        var modifiers = sort is null ? []
            : sort.Value.Direction == TableSortDirection.Ascending ? SortedAscending : SortedDescending;

        using (gui.StyledNode("table-header-cell", id: id, modifiers: modifiers)
                   .Width(width).Height(theme.HeaderHeight).Direction(Axis.Horizontal).Gap(4f)
                   .PaddingX(theme.CellPadding).ContentAlignX(AlignOf(column.Alignment)).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            gui.DrawText(column.Title, theme.FontSize, centerInRect: false, clip: true);

            if (sort is { } marked)
            {
                gui.DrawText(marked.Direction == TableSortDirection.Ascending
                    ? WidgetIcons.ChevronUp
                    : WidgetIcons.ChevronDown, theme.FontSize * 0.7f);
            }
        }
    }

    /// <summary>One body row: only the cells of the built columns, with spacers for the ones skipped.</summary>
    static void TableRow(Gui gui, TableState state, ITableViewSource source, TableTheme theme, TableLayout layout,
        int row, int firstColumn, int lastColumn)
    {
        var slot = state.Cache.Slot(row);
        using (gui.StyledNode("table-row", id: TableCache.RowId(slot), modifiers: row % 2 == 1 ? OddRow : [])
                   .Height(theme.RowHeight).Width(layout.Total).Direction(Axis.Horizontal).Enter())
        {
            VirtualRows.SpacerX(gui, TableCache.PadLeftId(slot), layout.Offsets[firstColumn], theme.RowHeight);
            for (var c = firstColumn; c < lastColumn; c++)
            {
                TableCell(gui, state, source, theme, slot, c, layout.Columns[c], layout.Widths[c]);
            }

            VirtualRows.SpacerX(gui, TableCache.PadRightId(slot), layout.Total - layout.Offsets[lastColumn],
                theme.RowHeight);
        }
    }

    static void TableCell(Gui gui, TableState state, ITableViewSource source, TableTheme theme, TableRowSlot slot,
        int index, TableColumn column, float width)
    {
        using (gui.StyledNode("table-cell", id: state.Cache.CellId(slot, index))
                   .Width(width).Height(theme.RowHeight).Direction(Axis.Horizontal).PaddingX(theme.CellPadding)
                   .ContentAlignX(AlignOf(column.Alignment)).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            var value = source.GetValue(slot.Row, column);
            if (column.Renderer is { } renderer)
            {
                renderer.Render(gui, new TableCellContext(slot.Row, column, value, theme.FontSize, width,
                    theme.RowHeight, source, state));
                return;
            }

            gui.DrawText(state.Cache.Text(slot, index, value), theme.FontSize, centerInRect: false, clip: true);
        }
    }

    static float AlignOf(TableAlignment alignment) => alignment switch
    {
        TableAlignment.Center => 0.5f,
        TableAlignment.Right => 1f,
        _ => 0f
    };
}

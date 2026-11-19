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
    /// The header row: the left-pinned cells, the scrolling cells, then the right-pinned cells. It sits outside
    /// the scrolling body, so it never leaves the top. Only the scrolling group takes the body's horizontal
    /// offset, so its cells stay over their columns while the pinned ones stay put.
    /// </summary>
    static void TableHeader(Gui gui, TableState state, TableTheme theme, TableLayout layout,
        int firstColumn, int lastColumn)
    {
        using (gui.StyledNode("table-header", id: "table/header").Height(theme.HeaderHeight).ExpandWidth()
                   .Direction(Axis.Horizontal).Enter())
        {
            gui.ClipContent();

            if (layout.LeftCount > 0)
            {
                PinnedHeader(gui, state, theme, layout, 0, layout.LeftCount, "table/header/pinned-left",
                    layout.LeftWidth);
            }

            // The offsets run from the table's left edge, so the spacers subtract what the pinned group holds.
            using (gui.StyledNode("table-header-scroll", id: "table/header/scroll").ExpandWidth()
                       .Height(theme.HeaderHeight).Direction(Axis.Horizontal).Enter())
            {
                gui.SetLocalScrollOffset(new Vector2(BodyScroll(gui, state).X, 0f));
                gui.ClipContent();

                VirtualRows.SpacerX(gui, "table/header/padLeft", layout.Offsets[firstColumn] - layout.LeftWidth,
                    theme.HeaderHeight);
                for (var c = firstColumn; c < lastColumn; c++)
                {
                    HeaderCell(gui, state, theme, layout.Columns[c], state.Cache.Columns!.HeaderIds[c], layout.Widths[c]);
                }

                VirtualRows.SpacerX(gui, "table/header/padRight",
                    layout.Offsets[layout.RightStart] - layout.Offsets[lastColumn], theme.HeaderHeight);
            }

            if (layout.RightStart < layout.Columns.Length)
            {
                PinnedHeader(gui, state, theme, layout, layout.RightStart, layout.Columns.Length,
                    "table/header/pinned-right", layout.RightWidth);

                // The body leaves room for its vertical bar when it shows one; the header leaves the same room
                // so its right-pinned group lines up with the body's.
                var scrollbar = gui.GetScrollState("table/body");
                if (scrollbar is { ShowScrollbarY: true })
                {
                    VirtualRows.SpacerX(gui, "table/header/scrollbar", scrollbar.ScrollbarThickness, theme.HeaderHeight);
                }
            }
        }
    }

    /// <summary>A header group whose columns are pinned: it does not scroll, so it keeps its own width.</summary>
    static void PinnedHeader(Gui gui, TableState state, TableTheme theme, TableLayout layout, int firstColumn,
        int lastColumn, string id, float width)
    {
        using (gui.StyledNode("table-header-pinned", id: id).Width(width).Height(theme.HeaderHeight)
                   .Direction(Axis.Horizontal).Enter())
        {
            for (var c = firstColumn; c < lastColumn; c++)
            {
                HeaderCell(gui, state, theme, layout.Columns[c], state.Cache.Columns!.HeaderIds[c], layout.Widths[c]);
            }
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
                   .PaddingX(theme.CellPadding).AlignContentX(column.AlignContentX).AlignContentY(column.AlignmentY).Enter())
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

    /// <summary>
    /// One body row: the built scrolling cells, with spacers for the ones skipped, then the pinned cells of the
    /// row over the body where the pinned columns sit.
    /// </summary>
    static void TableRow<TRow>(Gui gui, TableState state, ITableViewSource<TRow> source, TableColumn<TRow>[] shown,
        TableTheme theme, TableLayout layout, int row, int firstColumn, int lastColumn)
    {
        var slot = state.Cache.Slot(row);
        var item = source[row];
        var modifiers = row % 2 == 1 ? OddRow : [];
        using (gui.StyledNode("table-row", id: TableCache.RowId(slot), modifiers: modifiers)
                   .Height(theme.RowHeight).Width(layout.Total).Direction(Axis.Horizontal).Enter())
        {
            VirtualRows.SpacerX(gui, TableCache.PadLeftId(slot), layout.Offsets[firstColumn], theme.RowHeight);
            for (var c = firstColumn; c < lastColumn; c++)
            {
                TableCell(gui, state, source, theme, slot, item, c, shown[c], layout.Widths[c]);
            }

            VirtualRows.SpacerX(gui, TableCache.PadRightId(slot), layout.Total - layout.Offsets[lastColumn],
                theme.RowHeight);

            if (layout.LeftCount > 0 || layout.RightStart < shown.Length)
            {
                // An absolute child is placed from its parent's box before the scroll is applied, so the strips
                // take the body's offset themselves: sideways they stay at the body's edges, and down they follow the row.
                var scroll = BodyScroll(gui, state);
                if (layout.LeftCount > 0)
                {
                    PinnedStrip(gui, state, source, shown, theme, layout, slot, item, TablePin.Left, 0,
                        layout.LeftCount, new Vector2(0f, -scroll.Y), modifiers);
                }

                if (layout.RightStart < shown.Length)
                {
                    PinnedStrip(gui, state, source, shown, theme, layout, slot, item, TablePin.Right,
                        layout.RightStart, shown.Length,
                        new Vector2(state.FrameViewportWidth - layout.RightWidth, -scroll.Y), modifiers);
                }
            }
        }
    }

    /// <summary>
    /// The pinned cells of one row, laid over the body where the scrolling cells pass beneath them. The strip
    /// is a child of its row, placed at <paramref name="at"/> from the row's own box. It is opaque and blocks the
    /// pointer, so a cell scrolled beneath it gets no hover or click; the wheel over it is handed to the body.
    /// </summary>
    static void PinnedStrip<TRow>(Gui gui, TableState state, ITableViewSource<TRow> source, TableColumn<TRow>[] shown,
        TableTheme theme, TableLayout layout, TableRowSlot slot, TRow item, TablePin pin, int firstColumn,
        int lastColumn, Vector2 at, IReadOnlyList<string> modifiers)
    {
        var width = pin == TablePin.Left ? layout.LeftWidth : layout.RightWidth;
        using (gui.StyledNode("table-pinned", id: TableCache.PinnedId(slot, pin), modifiers: modifiers)
                   .Absolute(at.X, at.Y).Width(width).Height(theme.RowHeight).Direction(Axis.Horizontal)
                   .BlockInput().Enter())
        {
            for (var c = firstColumn; c < lastColumn; c++)
            {
                TableCell(gui, state, source, theme, slot, item, c, shown[c], layout.Widths[c]);
            }

            if (gui.Pass == Pass.Pass2Render)
            {
                ForwardWheel(gui);
            }
        }
    }

    /// <summary>
    /// The body's scroll offset as this frame's layout reads it. The state is the one the body applies to its
    /// rows, so the header and the pinned strips line up with the cells in the same frame.
    /// </summary>
    static Vector2 BodyScroll(Gui gui, TableState state) =>
        gui.GetScrollState("table/body")?.ScrollOffset ?? new Vector2(state.FrameScrollX, state.FrameScrollY);

    /// <summary>
    /// Scrolls the body by the wheel while the pointer is over a pinned strip. The strip blocks the body's own
    /// wheel handling there, so the strip passes the wheel on, one step at a time, as the body would take it.
    /// </summary>
    static void ForwardWheel(Gui gui)
    {
        var wheel = gui.Input.MouseWheelDelta;
        if (Math.Abs(wheel) <= 0.01f || !gui.CurrentNode.GetInteractable().OnHover())
        {
            return;
        }

        var shift = gui.Input.IsKeyDown(KeyboardKey.LeftShift) || gui.Input.IsKeyDown(KeyboardKey.RightShift);
        gui.ScrollBy("table/body", shift ? new Vector2(-wheel * 50f, 0f) : new Vector2(0f, -wheel * 50f));
    }

    static void TableCell<TRow>(Gui gui, TableState state, ITableViewSource<TRow> source, TableTheme theme,
        TableRowSlot slot, TRow item, int index, TableColumn<TRow> column, float width)
    {
        using (gui.StyledNode("table-cell", id: state.Cache.CellId(slot, index))
                   .Width(width).Height(theme.RowHeight).Direction(Axis.Horizontal).PaddingX(theme.CellPadding)
                   .AlignContentX(column.AlignContentX).AlignContentY(column.AlignmentY)
                   .Enter())
        {
            gui.ClipContent();
            column.RenderCell(gui, item, new TableCellFrame<TRow>(slot.Row, index, slot, state.Cache, source, state,
                theme.FontSize, width, theme.RowHeight));
        }
    }
}

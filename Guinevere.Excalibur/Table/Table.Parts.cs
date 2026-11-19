namespace Guinevere;

public static partial class ControlsExtensions
{
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

namespace Guinevere;

/// <summary>
/// Everything one frame of a table builds from, gathered once so the parts of the table pass a single value
/// instead of a dozen arguments.
/// </summary>
/// <typeparam name="TRow">The type of a row.</typeparam>
readonly record struct TableBuild<TRow>(Gui Gui, TableState State, ITableViewSource<TRow> Source,
    TableColumn<TRow>[] Shown, TableOptions Options, TableOptions<TRow>? Typed, TableTheme Theme, TableLayout Layout,
    int FirstColumn, int LastColumn);

public static partial class ControlsExtensions
{
    const int OddBit = 1;
    const int SelectedBit = 2;
    const int CurrentBit = 4;

    // Every combination of the row's states, so a row's modifiers are looked up and never built.
    static readonly string[][] RowModifierSets =
    [
        [], ["odd"], ["selected"], ["odd", "selected"],
        ["current"], ["odd", "current"], ["selected", "current"], ["odd", "selected", "current"]
    ];

    /// <summary>
    /// One body row: the built scrolling cells, with spacers for the ones skipped, then the pinned cells of the
    /// row over the body where the pinned columns sit, then the row's details when it is expanded.
    /// </summary>
    static void TableRow<TRow>(in TableBuild<TRow> b, int row)
    {
        var (gui, state, source, shown, _, _, theme, layout, firstColumn, lastColumn) = b;
        var slot = state.Cache.Slot(row);
        var key = TableCache.Key(slot, source);
        var item = source[row];
        var selection = state.Selection;
        var states = (row % 2 == 1 ? OddBit : 0) | (selection.Contains(key) ? SelectedBit : 0)
                                                 | (selection.CurrentKey == key ? CurrentBit : 0);
        var modifiers = RowModifierSets[states];
        using (gui.StyledNode("table-row", id: TableCache.RowId(slot), modifiers: modifiers)
                   .Height(theme.RowHeight).Width(layout.Total).Direction(Axis.Horizontal).Enter())
        {
            RowInteraction(b, row, key, item);
            PublishRowSemantics(b, slot, item, row, key);
            VirtualRows.SpacerX(gui, TableCache.PadLeftId(slot), layout.Offsets[firstColumn], theme.RowHeight);
            for (var c = firstColumn; c < lastColumn; c++)
            {
                TableCell(b, slot, item, c);
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
                    PinnedStrip(b, slot, item, TablePin.Left, 0, layout.LeftCount, new Vector2(0f, -scroll.Y),
                        modifiers, row, key);
                }

                if (layout.RightStart < shown.Length)
                {
                    PinnedStrip(b, slot, item, TablePin.Right, layout.RightStart, shown.Length,
                        new Vector2(state.FrameViewportWidth - layout.RightWidth, -scroll.Y), modifiers, row, key);
                }
            }
        }

        if (b.Typed?.RowDetails is { } details && state.Expansion.DetailHeight(key, row) is var height and > 0f)
        {
            RowDetails(b, details, slot, item, row, height);
        }
    }

    /// <summary>The content below an expanded row, in a node as wide as the columns and as tall as the provider said.</summary>
    static void RowDetails<TRow>(in TableBuild<TRow> b, ITableRowDetails<TRow> details, TableRowSlot slot, TRow item,
        int row, float height)
    {
        var gui = b.Gui;
        using (gui.StyledNode("table-details", id: TableCache.DetailsId(slot))
                   .Width(b.Layout.Total).Height(height).Direction(Axis.Vertical).Enter())
        {
            gui.ClipContent();
            details.Render(gui, new TableRowContext<TRow>(row, item, new RowIdentity(slot.Key!), b.State),
                b.Layout.Total, height);
        }
    }

    /// <summary>
    /// The pinned cells of one row, laid over the body where the scrolling cells pass beneath them. The strip
    /// is a child of its row, placed at <paramref name="at"/> from the row's own box. It is opaque and blocks the
    /// pointer, so a cell scrolled beneath it gets no hover or click of its own; the strip takes the row's click
    /// for it, and the wheel over it is handed to the body.
    /// </summary>
    static void PinnedStrip<TRow>(in TableBuild<TRow> b, TableRowSlot slot, TRow item, TablePin pin, int firstColumn,
        int lastColumn, Vector2 at, IReadOnlyList<string> modifiers, int row, string key)
    {
        var gui = b.Gui;
        var width = pin == TablePin.Left ? b.Layout.LeftWidth : b.Layout.RightWidth;
        using (gui.StyledNode("table-pinned", id: TableCache.PinnedId(slot, pin), modifiers: modifiers)
                   .Absolute(at.X, at.Y).Width(width).Height(b.Theme.RowHeight).Direction(Axis.Horizontal)
                   .BlockInput().Enter())
        {
            RowInteraction(b, row, key, item);
            for (var c = firstColumn; c < lastColumn; c++)
            {
                TableCell(b, slot, item, c);
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

    static void TableCell<TRow>(in TableBuild<TRow> b, TableRowSlot slot, TRow item, int index)
    {
        var gui = b.Gui;
        var column = b.Shown[index];
        var width = b.Layout.Widths[index];
        using (gui.StyledNode("table-cell", id: b.State.Cache.CellId(slot, index))
                   .Width(width).Height(b.Theme.RowHeight).Direction(Axis.Horizontal).PaddingX(b.Theme.CellPadding)
                   .AlignContentX(column.AlignContentX).AlignContentY(column.AlignmentY)
                   .Enter())
        {
            gui.ClipContent();
            column.RenderCell(gui, item, new TableCellFrame<TRow>(slot.Row, index, slot, b.State.Cache, b.Source,
                b.State, b.Theme.FontSize, width, b.Theme.RowHeight));
            PublishCellSemantics(b, slot, item, index);
        }
    }
}

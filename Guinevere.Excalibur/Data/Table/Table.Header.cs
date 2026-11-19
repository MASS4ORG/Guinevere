namespace Guinevere;

public static partial class ControlsExtensions
{
    const float ResizeHandleWidth = 6f;

    static readonly DragDropTag ColumnDrag = new("table-column");
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
    static void TableHeader<TRow>(in TableBuild<TRow> b)
    {
        var (gui, state, _, _, options, _, metrics, layout, firstColumn, lastColumn, _, enabled) = b;
        var header = gui.StyledNode("table-header", id: "table/header").ExpandWidth().Direction(Axis.Horizontal);
        SizeUnlessStyled(header, null, Size.Pixels(metrics.HeaderHeight));
        using (header.Enter())
        {
            gui.ClipContent();
            if (enabled && options.ColumnMenu && gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick(MouseButton.Right))
            {
                state.HeaderMenuOpen = true;
            }

            if (layout.LeftCount > 0)
            {
                PinnedHeader(b, 0, layout.LeftCount, "table/header/pinned-left", layout.LeftWidth);
            }

            // The offsets run from the table's left edge, so the spacers subtract what the pinned group holds.
            using (gui.StyledNode("table-header-scroll", id: "table/header/scroll").ExpandWidth()
                       .Height(metrics.HeaderHeight).Direction(Axis.Horizontal).Enter())
            {
                gui.SetLocalScrollOffset(new Vector2(BodyScroll(gui, state).X, 0f));
                gui.ClipContent();

                VirtualRows.SpacerX(gui, "table/header/padLeft", layout.Offsets[firstColumn] - layout.LeftWidth,
                    metrics.HeaderHeight);
                for (var c = firstColumn; c < lastColumn; c++)
                {
                    HeaderCell(b, c);
                }

                VirtualRows.SpacerX(gui, "table/header/padRight",
                    layout.Offsets[layout.RightStart] - layout.Offsets[lastColumn], metrics.HeaderHeight);
            }

            if (layout.RightStart < layout.Columns.Length)
            {
                PinnedHeader(b, layout.RightStart, layout.Columns.Length, "table/header/pinned-right",
                    layout.RightWidth);

                // The body leaves room for its vertical bar when it shows one; the header leaves the same room
                // so its right-pinned group lines up with the body's.
                var scrollbar = gui.GetScrollState(BodyId);
                if (scrollbar is { ShowScrollbarY: true })
                {
                    VirtualRows.SpacerX(gui, "table/header/scrollbar", scrollbar.ScrollbarThickness, metrics.HeaderHeight);
                }
            }
        }

        if (state.HeaderMenuOpen)
        {
            ColumnMenu(b);
        }
    }

    /// <summary>A header group whose columns are pinned: it does not scroll, so it keeps its own width.</summary>
    static void PinnedHeader<TRow>(in TableBuild<TRow> b, int firstColumn, int lastColumn, string id, float width)
    {
        var gui = b.Gui;
        var pinned = gui.StyledNode("table-header-pinned", id: id).Width(width).Direction(Axis.Horizontal);
        SizeUnlessStyled(pinned, null, Size.Pixels(b.Metrics.HeaderHeight));
        using (pinned.Enter())
        {
            for (var c = firstColumn; c < lastColumn; c++)
            {
                HeaderCell(b, c);
            }
        }
    }

    /// <summary>The menu a right-click on the header opens: every column with a check, and a way back to the defaults.</summary>
    static void ColumnMenu<TRow>(in TableBuild<TRow> b)
    {
        var state = b.State;
        var reset = b.Options.ResetColumnsText;
        b.Gui.ContextMenu(ref state.HeaderMenuOpen, menu =>
        {
            foreach (var column in state.Cache.Columns!.All)
            {
                var captured = column;
                menu.Items.Add(new FlyoutItem
                {
                    Content = column.Title.Length > 0 || column.Header.IsEmpty ? column.Name : column.Header,
                    Key = column.Id,
                    IsChecked = () => state.Columns.IsVisible(captured),
                    OnCheckChanged = visible => state.Columns.SetVisible(captured, visible)
                });
            }

            menu.Separator().Item(reset, state.Columns.Reset);
        }, id: "table/header/menu-popup");
    }

    static void HeaderCell<TRow>(in TableBuild<TRow> b, int index)
    {
        var (gui, state, _, _, _, _, metrics, layout, _, _, _, _) = b;
        var column = layout.Columns[index];
        var id = state.Cache.View!.HeaderIds[index];
        var width = layout.Widths[index];
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

        var cell = gui.StyledNode("table-header-cell", column.Classes, id, modifiers)
            .Width(width).Direction(Axis.Horizontal).Gap(4f);
        SizeUnlessStyled(cell, null, Size.Pixels(metrics.HeaderHeight));
        PadUnlessStyled(cell, metrics.CellPadding, 0f);
        CenterVerticallyUnlessStyled(cell);
        using (cell.Enter())
        {
            gui.ClipContent();
            if (!column.Header.IsEmpty)
            {
                gui.SetTextSize(metrics.FontSize);
                gui.Content(column.Header);
            }
            else if (column.Title.Length > 0)
            {
                gui.DrawText(column.Title, metrics.FontSize, centerInRect: false, clip: true);
            }

            if (position == 0)
            {
                gui.DrawText(ascending ? WidgetIcons.ChevronUp : WidgetIcons.ChevronDown, metrics.FontSize * 0.7f);
                if (more > 0)
                {
                    gui.DrawText(more <= MoreLabels.Length ? MoreLabels[more - 1] : "+" + more, metrics.FontSize * 0.7f);
                }
            }

            HeaderInteraction(b, index, id);
        }
    }

    /// <summary>
    /// What the user can do with a header cell: click it (or press Enter or Space on it) to sort by it, drag it
    /// onto another to reorder, press Ctrl with an arrow to move it, Shift with an arrow to resize it, and drag
    /// its right edge to resize it.
    /// </summary>
    static void HeaderInteraction<TRow>(in TableBuild<TRow> b, int index, string id)
    {
        var (gui, state, source, _, options, _, metrics, layout, _, _, _, enabled) = b;
        var column = layout.Columns[index];
        var resizable = options.Resizable && column.Resizable;
        var view = state.Cache.View!;
        if (resizable)
        {
            ResizeHandle(gui, state, metrics, layout, index, view.ResizeId(index), view.AnchorId(index), enabled);
        }

        if (gui.Pass != Pass.Pass2Render || !enabled)
        {
            return;
        }

        gui.Focus.RegisterFocusableControl(id, gui.CurrentNode.Rect.Center);
        if (options.Reorderable)
        {
            HeaderReorder(gui, state, column, id, view.Ghosts[index]);
        }

        var rect = gui.CurrentNode.Rect;
        var overEdge = resizable && gui.Input.MousePosition.X >= rect.X + rect.W - ResizeHandleWidth;
        if (gui.GetInteractable().OnClickCompleted(out _) && !overEdge)
        {
            gui.RequestFocus(FocusReason.Mouse);
            SortByHeader(state, source, column);
        }

        // Shift or Ctrl with an arrow is this header's, so it does not also move focus to the next header.
        gui.Focus.SetClaimsArrowKeys(id, IsModifierDown(gui, KeyboardKey.LeftShift, KeyboardKey.RightShift)
                                         || IsModifierDown(gui, KeyboardKey.LeftControl, KeyboardKey.RightControl));

        if (gui.HasFocus(id))
        {
            HeaderKeys(b, index, resizable, column);
        }
    }

    /// <summary>Sorts the way a click does when the source can sort and the column can be sorted; else nothing.</summary>
    static void SortByHeader<TRow>(TableState state, ITableViewSource<TRow> source, TableColumn column)
    {
        if (column.Sortable && source is ISortableTableViewSource<TRow> sortable)
        {
            state.SortBy(sortable, column);
        }
    }

    /// <summary>
    /// The cell as a drag source, and while a column is being dragged a drop target for it. The target is offered
    /// only then, so a table nobody is dragging in pays nothing for it.
    /// </summary>
    static void HeaderReorder(Gui gui, TableState state, TableColumn column, string id, Action<Gui> ghost)
    {
        gui.DragSource<TableColumn>(id, column, ghost: ghost, tag: ColumnDrag, keyboard: false);
        if (!gui.IsDragging)
        {
            return;
        }

        var drop = gui.DropTarget<TableColumn>(id + "/drop", ColumnDrag,
            canAccept: dragged => state.Cache.Columns!.Contains(dragged) && dragged.Pin == column.Pin,
            onDrop: dragged => state.Columns.MoveTo(dragged, column), keyboard: false);
        gui.DrawDropIndicator(drop.State);
    }

    /// <summary>
    /// Enter or Space sorts by the focused column, Shift and an arrow resize it by a step, and Ctrl and an arrow
    /// move it one place.
    /// </summary>
    static void HeaderKeys<TRow>(in TableBuild<TRow> b, int index, bool resizable, TableColumn column)
    {
        var (gui, state, source, _, options, _, _, layout, _, _, _, _) = b;
        var input = gui.Input;
        if (input.IsKeyPressed(KeyboardKey.Enter) || input.IsKeyPressed(KeyboardKey.Space))
        {
            SortByHeader(state, source, column);
            return;
        }

        var direction = input.IsKeyPressed(KeyboardKey.Left) ? -1 : input.IsKeyPressed(KeyboardKey.Right) ? 1 : 0;
        if (direction == 0)
        {
            return;
        }

        if (IsModifierDown(gui, KeyboardKey.LeftShift, KeyboardKey.RightShift))
        {
            if (resizable)
            {
                state.Columns.ResizeByStep(layout, index, direction);
            }
        }
        else if (options.Reorderable && IsModifierDown(gui, KeyboardKey.LeftControl, KeyboardKey.RightControl))
        {
            state.Columns.Nudge(column, direction);
        }
    }

    static bool IsModifierDown(Gui gui, KeyboardKey left, KeyboardKey right) =>
        gui.Input.IsKeyDown(left) || gui.Input.IsKeyDown(right);

    /// <summary>
    /// The grab zone on a header's right edge. Dragging it sets the column's width to the width it had at the
    /// press plus the pointer's travel; a double click gives the column its defined width back.
    /// </summary>
    static void ResizeHandle(Gui gui, TableState state, TableMetrics metrics, TableLayout layout, int index, string id,
        string anchorKey, bool enabled)
    {
        // An absolute child is placed from the cell's padding box, so the cell's own padding is taken off.
        var width = layout.Widths[index];
        using (gui.StyledNode("table-header-resize", id: id)
                   .Absolute(Math.Max(0f, width - ResizeHandleWidth - metrics.CellPadding), 0f)
                   .Width(ResizeHandleWidth)
                   .Height(metrics.HeaderHeight).Cursor(PointerCursor.ResizeHorizontal).Enter())
        {
            ref var anchor = ref gui.GetValue(float.NaN, anchorKey);
            if (gui.Pass != Pass.Pass2Render || !enabled)
            {
                return;
            }

            var interactable = gui.GetInteractable();
            if (interactable.OnClick(out var clicks) && clicks >= 2)
            {
                state.Columns.ResetWidth(layout.Columns[index]);
            }

            if (!interactable.OnDrag(out var drag))
            {
                anchor = float.NaN;
                return;
            }

            if (float.IsNaN(anchor))
            {
                anchor = width;
            }

            state.Columns.Resize(layout, index, anchor + drag.TotalDelta.X);
        }
    }
}

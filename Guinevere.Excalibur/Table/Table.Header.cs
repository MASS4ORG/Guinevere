namespace Guinevere;

public static partial class ControlsExtensions
{
    const string HeaderMenuKey = "table/header/menu";
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
    static void TableHeader(Gui gui, TableState state, TableOptions options, TableTheme theme, TableLayout layout,
        int firstColumn, int lastColumn)
    {
        using (gui.StyledNode("table-header", id: "table/header").Height(theme.HeaderHeight).ExpandWidth()
                   .Direction(Axis.Horizontal).Enter())
        {
            gui.ClipContent();
            if (options.ColumnMenu && gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnClick(MouseButton.Right))
            {
                gui.GetValue(false, HeaderMenuKey) = true;
            }

            if (layout.LeftCount > 0)
            {
                PinnedHeader(gui, state, options, theme, layout, 0, layout.LeftCount, "table/header/pinned-left",
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
                    HeaderCell(gui, state, options, theme, layout, c);
                }

                VirtualRows.SpacerX(gui, "table/header/padRight",
                    layout.Offsets[layout.RightStart] - layout.Offsets[lastColumn], theme.HeaderHeight);
            }

            if (layout.RightStart < layout.Columns.Length)
            {
                PinnedHeader(gui, state, options, theme, layout, layout.RightStart, layout.Columns.Length,
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

        if (options.ColumnMenu)
        {
            ColumnMenu(gui, state, options);
        }
    }

    /// <summary>A header group whose columns are pinned: it does not scroll, so it keeps its own width.</summary>
    static void PinnedHeader(Gui gui, TableState state, TableOptions options, TableTheme theme, TableLayout layout,
        int firstColumn, int lastColumn, string id, float width)
    {
        using (gui.StyledNode("table-header-pinned", id: id).Width(width).Height(theme.HeaderHeight)
                   .Direction(Axis.Horizontal).Enter())
        {
            for (var c = firstColumn; c < lastColumn; c++)
            {
                HeaderCell(gui, state, options, theme, layout, c);
            }
        }
    }

    /// <summary>The menu a right-click on the header opens: every column with a check, and a way back to the defaults.</summary>
    static void ColumnMenu(Gui gui, TableState state, TableOptions options)
    {
        ref var open = ref gui.GetValue(false, HeaderMenuKey);
        gui.ContextMenu(ref open, menu =>
        {
            foreach (var column in state.Cache.Columns!.All)
            {
                var captured = column;
                menu.Items.Add(new FlyoutItem
                {
                    Text = column.Title.Length > 0 ? column.Title : column.Id,
                    IsChecked = () => state.Columns.IsVisible(captured),
                    OnCheckChanged = visible => state.Columns.SetVisible(captured, visible)
                });
            }

            menu.Separator().Item(options.ResetColumnsText, state.Columns.Reset);
        }, id: "table/header/menu-popup");
    }

    static void HeaderCell(Gui gui, TableState state, TableOptions options, TableTheme theme, TableLayout layout,
        int index)
    {
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

            HeaderInteraction(gui, state, options, theme, layout, index, id);
        }
    }

    /// <summary>
    /// What the user can do with a header cell: focus it, drag it onto another to reorder, press Ctrl with an
    /// arrow to move it, Shift with an arrow to resize it, and drag its right edge to resize it.
    /// </summary>
    static void HeaderInteraction(Gui gui, TableState state, TableOptions options, TableTheme theme,
        TableLayout layout, int index, string id)
    {
        var column = layout.Columns[index];
        var resizable = options.Resizable && column.Resizable;
        if (resizable)
        {
            ResizeHandle(gui, state, theme, layout, index, id + "/resize");
        }

        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        if (options.Reorderable)
        {
            HeaderReorder(gui, state, column, id);
        }
        else
        {
            gui.Focus.RegisterFocusableControl(id, gui.CurrentNode.Rect.Center);
        }

        if (gui.GetInteractable().OnClick())
        {
            gui.RequestFocus(FocusReason.Mouse);
        }

        // Shift or Ctrl with an arrow is this header's, so it does not also move focus to the next header.
        gui.Focus.SetClaimsArrowKeys(id, IsModifierDown(gui, KeyboardKey.LeftShift, KeyboardKey.RightShift)
                                         || IsModifierDown(gui, KeyboardKey.LeftControl, KeyboardKey.RightControl));

        if (gui.HasFocus(id))
        {
            HeaderKeys(gui, state, options, layout, index, resizable);
        }
    }

    /// <summary>The cell as a drag source and a drop target for columns of this table.</summary>
    static void HeaderReorder(Gui gui, TableState state, TableColumn column, string id)
    {
        gui.DragSource(id, column, ghost: ghost => ghost.DrawText(column.Title, 12f), tag: ColumnDrag);
        var drop = gui.DropTarget<TableColumn>(id + "/drop", ColumnDrag,
            canAccept: dragged => state.Cache.Columns!.Contains(dragged) && dragged.Pin == column.Pin,
            onDrop: dragged => state.Columns.MoveTo(dragged, column));
        gui.DrawDropIndicator(drop.State);
    }

    /// <summary>Shift and an arrow resize the focused column by a step, Ctrl and an arrow move it one place.</summary>
    static void HeaderKeys(Gui gui, TableState state, TableOptions options, TableLayout layout, int index,
        bool resizable)
    {
        var input = gui.Input;
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
            state.Columns.Nudge(layout.Columns[index], direction);
        }
    }

    static bool IsModifierDown(Gui gui, KeyboardKey left, KeyboardKey right) =>
        gui.Input.IsKeyDown(left) || gui.Input.IsKeyDown(right);

    /// <summary>
    /// The grab zone on a header's right edge. Dragging it sets the column's width to the width it had at the
    /// press plus the pointer's travel; a double click gives the column its defined width back.
    /// </summary>
    static void ResizeHandle(Gui gui, TableState state, TableTheme theme, TableLayout layout, int index, string id)
    {
        // An absolute child is placed from the cell's padding box, so the cell's own padding is taken off.
        var width = layout.Widths[index];
        using (gui.StyledNode("table-header-resize", id: id)
                   .Absolute(Math.Max(0f, width - ResizeHandleWidth - theme.CellPadding), 0f)
                   .Width(ResizeHandleWidth)
                   .Height(theme.HeaderHeight).Cursor(PointerCursor.ResizeHorizontal).Enter())
        {
            ref var anchor = ref gui.GetValue(float.NaN, id + "/anchor");
            if (gui.Pass != Pass.Pass2Render)
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

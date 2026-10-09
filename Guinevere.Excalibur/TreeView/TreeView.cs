using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Rows kept built above and below the viewport, so a fast scroll has no gap.</summary>
    const int Overscan = 4;

    /// <summary>
    /// Draws a scrollable tree from a flattened row list. Rows under a collapsed parent are skipped by
    /// depth, and only the rows inside the viewport become layout nodes, so a tree of thousands costs
    /// the same as one that fills the screen.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="state">Expansion and selection, kept by the caller between frames.</param>
    /// <param name="items">Every row of the tree, parents before their children.</param>
    /// <param name="theme">Layout and interaction metrics. Defaults to <see cref="TreeViewTheme.Default"/>.</param>
    /// <param name="onClick">Called for a click on a row, with the button and the click count.</param>
    /// <param name="dragPayload">
    /// Supplies what a row carries when dragged, or null for a tree whose rows are not drag sources.
    /// Returning null for a given row leaves that row undraggable. A row carries the payload under its
    /// runtime type, so a typed <c>DropTarget&lt;T&gt;</c> elsewhere in the frame can accept it.
    /// </param>
    /// <param name="onRename">
    /// Receives the new name when an inline rename started with <see cref="TreeViewState.BeginRename"/>
    /// is confirmed. Null leaves rows uneditable.
    /// </param>
    /// <param name="onEmptyClick">Called when the tree background receives a right click.</param>
    /// <param name="dropAccept">Whether a payload may be dropped on a row.</param>
    /// <param name="onDrop">Receives the row and payload after a successful drop.</param>
    /// <param name="classes">Stylesheet classes for this control.</param>
    /// <param name="id">Stable control and stylesheet identity.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void TreeView(this Gui gui, TreeViewState state, IReadOnlyList<TreeItem> items,
        TreeViewTheme? theme = null, Action<TreeViewEvent>? onClick = null,
        Func<TreeItem, object?>? dragPayload = null,
        Action<TreeItem, string>? onRename = null,
        Action<MouseButton>? onEmptyClick = null,
        Func<object, bool>? dropAccept = null,
        Action<TreeItem, object>? onDrop = null,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(items);
        ExcaliburStyles.Ensure(gui);

        theme ??= TreeViewTheme.Default;
        var visible = Flatten(items, state);
        state.VisibleIds = [.. visible.Select(item => item.Id)];

        using (gui.StyledNode("treeview", classes, id, filePath: filePath, lineNumber: lineNumber)
                   .Expand().Direction(Axis.Vertical).Enter())
        {
            gui.ScrollY();

            if (gui.Pass == Pass.Pass2Render)
                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true, claimsArrowKeys: true);

            // Both passes must pick the same rows, and a node's rect is only resolved in the render
            // pass — so the window comes from what the previous frame measured.
            var first = Math.Max(0, (int)(state.FrameScrollY / theme.RowHeight) - Overscan);
            var take = (int)(state.FrameViewportHeight / theme.RowHeight) + (Overscan * 2) + 1;
            var last = Math.Min(visible.Count, first + take);
            var rowClicked = false;
            void RowClick(TreeViewEvent e)
            {
                rowClicked = true;
                onClick?.Invoke(e);
            }

            Spacer(gui, "treeview/padTop", first * theme.RowHeight);

            for (var i = first; i < last; i++)
                RenderRow(gui, state, theme, visible[i], i, RowClick, dragPayload, dropAccept, onDrop, onRename);

            Spacer(gui, "treeview/padBottom", (visible.Count - last) * theme.RowHeight);

            if (gui.Pass != Pass.Pass2Render) return;

            ProcessTreeInput(gui, state, theme, visible, rowClicked, onClick, onEmptyClick);
        }
    }

    static void ProcessTreeInput(Gui gui, TreeViewState state, TreeViewTheme theme,
        List<TreeItem> visible, bool rowClicked, Action<TreeViewEvent>? onClick,
        Action<MouseButton>? onEmptyClick)
    {
        Measure(gui, state);
        if (!rowClicked)
        {
            var tree = gui.GetInteractable();
            if (tree.OnClick(MouseButton.Right))
                onEmptyClick?.Invoke(MouseButton.Right);
        }

        if (state.WantsReveal)
        {
            ScrollToSelection(gui, state, theme, visible);
            state.WantsReveal = false;
        }

        // Focus belongs to the tree, not the row that was clicked, so it is claimed here where the
        // container is the current node.
        if (state.WantsFocus)
        {
            gui.RequestFocus(FocusReason.Mouse);
            state.WantsFocus = false;
        }

        // Several trees can be on screen at once; only the focused one answers the arrow keys.
        if (gui.HasFocus()) Navigate(gui, state, theme, visible, onClick);
    }

    /// <summary>
    /// Drops the rows hidden under a collapsed parent. A collapsed row hides everything after it that
    /// is deeper, which is what makes a flat list enough to describe a tree.
    /// </summary>
    static List<TreeItem> Flatten(IReadOnlyList<TreeItem> items, TreeViewState state)
    {
        var visible = new List<TreeItem>(items.Count);
        var hiddenBelow = int.MaxValue;

        foreach (var item in items)
        {
            if (item.Depth > hiddenBelow) continue;

            hiddenBelow = int.MaxValue;
            visible.Add(item);

            if (item.HasChildren && state.IsCollapsed(item.Id, item.Depth)) hiddenBelow = item.Depth;
        }

        return visible;
    }

    /// <summary>
    /// Moves the selection with the arrow keys: up and down walk the visible rows, right opens a row
    /// or steps into it, left closes it or steps out to the parent, Enter reports an activation.
    /// </summary>
    static void Navigate(Gui gui, TreeViewState state, TreeViewTheme theme,
        List<TreeItem> visible, Action<TreeViewEvent>? onClick)
    {
        if (visible.Count == 0) return;

        var index = state.SelectedId is null
            ? -1
            : visible.FindIndex(item => item.Id == state.SelectedId);

        var previous = state.SelectedId;
        var previousIds = state.SelectedIds;
        var previousAnchor = state.SelectionAnchor;
        if (state.MultiSelect && ControlHeld(gui) && gui.Input.IsKeyPressed(KeyboardKey.A))
        {
            state.SetSelection(state.VisibleIds, previous);
            return;
        }
        if (!NavigateKeys(gui, state, visible, index, onClick)) return;

        ExtendKeyboardSelection(gui, state, previous, previousIds, previousAnchor);
        ScrollToSelection(gui, state, theme, visible);
    }

    static bool NavigateKeys(Gui gui, TreeViewState state, List<TreeItem> visible, int index,
        Action<TreeViewEvent>? onClick)
    {
        if (NavigateVertical(gui, state, visible, index)) return true;
        if (gui.Input.IsKeyPressed(KeyboardKey.Right)) Open(state, visible, index);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Left)) Close(state, visible, index);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Enter) && index >= 0)
            onClick?.Invoke(new TreeViewEvent(visible[index], MouseButton.Left, 2));
        else return false;
        return true;
    }

    static bool NavigateVertical(Gui gui, TreeViewState state, List<TreeItem> visible, int index)
    {
        if (gui.Input.IsKeyPressed(KeyboardKey.Down)) Select(state, visible, Math.Min(index + 1, visible.Count - 1));
        else if (gui.Input.IsKeyPressed(KeyboardKey.Up)) Select(state, visible, Math.Max(index - 1, 0));
        else if (gui.Input.IsKeyPressed(KeyboardKey.Home)) Select(state, visible, 0);
        else if (gui.Input.IsKeyPressed(KeyboardKey.End)) Select(state, visible, visible.Count - 1);
        else return false;
        return true;
    }

    static void ExtendKeyboardSelection(Gui gui, TreeViewState state, string? previous,
        IReadOnlyList<string> previousIds, string? previousAnchor)
    {
        if (state.MultiSelect && state.SelectedId is { } next && next != previous)
        {
            state.SetSelection(previousIds, previous);
            state.SelectionAnchor = previousAnchor;
            state.Select(next, state.VisibleIds, ControlHeld(gui), ShiftHeld(gui));
        }
    }

    static bool ControlHeld(Gui gui) =>
        gui.Input.IsKeyDown(KeyboardKey.LeftControl) || gui.Input.IsKeyDown(KeyboardKey.RightControl)
        || gui.Input.IsKeyDown(KeyboardKey.LeftSuper) || gui.Input.IsKeyDown(KeyboardKey.RightSuper);

    static bool ShiftHeld(Gui gui) =>
        gui.Input.IsKeyDown(KeyboardKey.LeftShift) || gui.Input.IsKeyDown(KeyboardKey.RightShift);

    static void Select(TreeViewState state, List<TreeItem> visible, int index)
    {
        if (index < 0 || index >= visible.Count) return;

        state.SelectedId = visible[index].Id;
    }

    static void Open(TreeViewState state, List<TreeItem> visible, int index)
    {
        if (index < 0) return;

        var item = visible[index];
        if (item.HasChildren && state.IsCollapsed(item.Id, item.Depth)) state.SetExpanded(item.Id, true);
        else Select(state, visible, index + 1);
    }

    static void Close(TreeViewState state, List<TreeItem> visible, int index)
    {
        if (index < 0) return;

        var item = visible[index];
        if (item.HasChildren && !state.IsCollapsed(item.Id, item.Depth))
        {
            state.SetExpanded(item.Id, false);
            return;
        }

        // Otherwise step out to the nearest shallower row, which is the parent.
        for (var i = index - 1; i >= 0; i--)
            if (visible[i].Depth < item.Depth)
            {
                state.SelectedId = visible[i].Id;
                return;
            }
    }

    /// <summary>Keeps the selected row inside the viewport after a keyboard move.</summary>
    static void ScrollToSelection(Gui gui, TreeViewState state, TreeViewTheme theme, List<TreeItem> visible)
    {
        var index = visible.FindIndex(item => item.Id == state.SelectedId);
        if (index < 0) return;

        var top = index * theme.RowHeight;
        var bottom = top + theme.RowHeight;

        var target = state.FrameScrollY;
        if (top < target) target = top;
        else if (bottom > target + state.FrameViewportHeight) target = bottom - state.FrameViewportHeight;

        if (Math.Abs(target - state.FrameScrollY) < 0.5f) return;

        gui.SetScrollPercentage(gui.CurrentNode.Id, Axis.Vertical,
            Math.Clamp(target / Math.Max(1f, (visible.Count * theme.RowHeight) - state.FrameViewportHeight), 0f, 1f));
        state.FrameScrollY = target;
    }

    /// <summary>
    /// Records the viewport and scroll offset for the next frame to virtualize against.
    /// </summary>
    static void Measure(Gui gui, TreeViewState state)
    {
        var rect = gui.CurrentNode.Rect;
        if (rect.H > 0) state.FrameViewportHeight = rect.H;

        state.FrameScrollY = gui.GetScrollState(gui.CurrentNode.Id) is { } scroll ? scroll.ScrollOffset.Y : 0f;
    }

    static void Spacer(Gui gui, string id, float height)
    {
        if (height <= 0) return;

        using (gui.Node(-1, height, id).ExpandWidth().Enter())
        {
        }
    }

    static void RenderRow(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item, int row,
        Action<TreeViewEvent>? onClick, Func<TreeItem, object?>? dragPayload,
        Func<object, bool>? dropAccept, Action<TreeItem, object>? onDrop,
        Action<TreeItem, string>? onRename)
    {
        var isSelected = state.SelectedIds.Contains(item.Id);
        var isEditing = onRename is not null && state.EditingId == item.Id;

        using (gui.StyledNode("tree-row", id: $"treeview/row{row}",
                       modifiers: isSelected ? ["selected"] : []).Height(theme.RowHeight)
                   .ExpandWidth()
                   .Direction(Axis.Horizontal)
                   .Padding((item.Depth * theme.IndentWidth) + theme.ContentPadding, 0)
                   .Gap(4f)
                   .Enter())
        {
            if (!isEditing && dragPayload?.Invoke(item) is { } payload)
                // `object` on purpose: this control only ever sees the payload untyped, and the source
                // resolves that to the payload's own type, so a typed target elsewhere in the frame —
                // an inspector field, say — can accept a row.
                gui.DragSource<object>($"treeview/row/{item.Id}", payload,
                    ghost: g => DragGhost(g, theme, item));

            if (gui.Pass == Pass.Pass2Render)
                RenderRowInput(gui, state, theme, item, isSelected, isEditing, onClick, dropAccept, onDrop);

            Expander(gui, state, theme, item, row);

            if (item.Icon is { } icon)
                using (gui.Node(theme.IconSize, theme.RowHeight, $"treeview/row{row}/icon").ContentAlignX(0.5f)
                           .ContentAlignY(0.5f).Enter())
                    icon(gui);

            RenderRowLabel(gui, state, theme, item, isEditing, isSelected, onRename);
        }
    }

    static void RenderRowLabel(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item,
        bool isEditing, bool isSelected, Action<TreeItem, string>? onRename)
    {
        if (isEditing) RenameBox(gui, state, theme, item, onRename!);
        else
            // A tint may match the selection fill, so a selected row always reads in the text color.
            gui.DrawText(item.Label, theme.FontSize, isSelected ? null : item.Tint,
                centerInRect: false);
    }

    /// <summary>
    /// The inline rename field. Enter commits, Escape abandons, and a click anywhere else commits too —
    /// the same bargain a file manager makes, so the box can never be left open by accident.
    /// </summary>
    static void RenameBox(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item,
        Action<TreeItem, string> onRename)
    {
        var text = state.EditingText;
        gui.TextInput(ref text, width: 0, height: theme.RowHeight - 2f, fontSize: theme.FontSize,
            padding: 3f, id: $"treeview/rename/{item.Id}", grabFocus: true);
        state.EditingText = text;

        if (gui.Pass != Pass.Pass2Render) return;

        var clickedAway = gui.Input.IsMouseButtonPressed(MouseButton.Left)
                          && !gui.CurrentNode.Rect.Contains(gui.Input.MousePosition);

        if (gui.Input.IsKeyPressed(KeyboardKey.Escape))
        {
            state.CancelRename();
            return;
        }

        if (!gui.Input.IsKeyPressed(KeyboardKey.Enter) && !clickedAway) return;

        var committed = state.EditingText;
        state.CancelRename();
        onRename(item, committed);
    }

    /// <summary>What follows the pointer while a row is dragged: the row's own label on a chip.</summary>
    static void DragGhost(Gui gui, TreeViewTheme theme, TreeItem item)
    {
        using (gui.StyledNode("tree-ghost").Height(theme.RowHeight).Padding(6, 0).ContentAlignY(0.5f).Enter())
        {
            gui.DrawText(item.Label, theme.FontSize, centerInRect: false);
        }
    }

    static void RenderRowInput(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item,
        bool isSelected, bool isEditing, Action<TreeViewEvent>? onClick,
        Func<object, bool>? dropAccept, Action<TreeItem, object>? onDrop)
    {
        if (dropAccept is not null)
        {
            var drop = gui.DropTarget($"treeview/drop/{item.Id}",
                canAccept: dropAccept, onDrop: payload => onDrop?.Invoke(item, payload));
            gui.DrawDropIndicator(drop.State, style: ExcaliburStyles.DroppableArea(gui));
        }

        var interactable = gui.GetInteractable();

        if (!isEditing)
        {
            Report(gui, state, theme, item, interactable, MouseButton.Left, onClick);
            Report(gui, state, theme, item, interactable, MouseButton.Right, onClick);
            Report(gui, state, theme, item, interactable, MouseButton.Middle, onClick);
        }
    }

    /// <summary>
    /// Reports a click on a row. The left button settles when the button comes back up, so a press
    /// that goes on to be a drag never selects the row; right and middle have no drag to be confused
    /// with, and a context menu wants its press immediately, so they stay on the press edge.
    /// </summary>
    static void Report(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item,
        InteractableElement interactable, MouseButton button, Action<TreeViewEvent>? onClick)
    {
        int clicks;
        var clicked = button == MouseButton.Left
            ? interactable.OnClickCompleted(out clicks, button, theme.DragThreshold)
            : interactable.OnClick(out clicks, button);
        if (!clicked) return;

        if (button == MouseButton.Left)
        {
            state.Select(item.Id, state.VisibleIds, ControlHeld(gui), ShiftHeld(gui));
            state.WantsFocus = true;

            // Single click selects, double click folds — the arrow is the one-click shortcut.
            if (clicks >= 2 && item.HasChildren) state.Toggle(item.Id, item.Depth);
        }

        onClick?.Invoke(new TreeViewEvent(item, button, clicks));
    }

    static void Expander(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item, int row)
    {
        // Blocks the row underneath, so clicking the arrow neither selects nor double-toggles.
        using (gui.StyledNode("tree-expander", id: $"treeview/row{row}/expander",
                       modifiers: state.IsCollapsed(item.Id, item.Depth) ? [] : ["expanded"])
                   .Width(theme.ExpanderWidth).Height(theme.RowHeight)
                   .BlockInput(item.HasChildren)
                   .ContentAlignX(0.5f).ContentAlignY(0.5f)
                   .Enter())
        {
            if (!item.HasChildren) return;

            var interactable = gui.GetInteractable();
            gui.DrawText(state.IsCollapsed(item.Id, item.Depth) ? WidgetIcons.ChevronRight : WidgetIcons.ChevronDown,
                theme.FontSize * 0.7f);

            if (gui.Pass == Pass.Pass2Render && interactable.OnClick()) state.Toggle(item.Id, item.Depth);
        }
    }
}

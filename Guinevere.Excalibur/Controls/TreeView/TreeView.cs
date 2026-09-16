using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Rows kept built above and below the viewport, so a fast scroll has no gap.</summary>
    private const int Overscan = 4;

    /// <summary>
    /// Draws a scrollable tree from a flattened row list. Rows under a collapsed parent are skipped by
    /// depth, and only the rows inside the viewport become layout nodes, so a tree of thousands costs
    /// the same as one that fills the screen.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="state">Expansion and selection, kept by the caller between frames.</param>
    /// <param name="items">Every row of the tree, parents before their children.</param>
    /// <param name="theme">Colors and metrics. Defaults to <see cref="TreeViewTheme.Default"/>.</param>
    /// <param name="onClick">Called for a click on a row, with the button and the click count.</param>
    /// <param name="dragPayload">
    /// Supplies what a row carries when dragged, or null for a tree whose rows are not drag sources.
    /// Returning null for a given row leaves that row undraggable.
    /// </param>
    /// <param name="onRename">
    /// Receives the new name when an inline rename started with <see cref="TreeViewState.BeginRename"/>
    /// is confirmed. Null leaves rows uneditable.
    /// </param>
    /// <param name="onEmptyClick">Called when the tree background receives a right click.</param>
    /// <param name="dropAccept">Whether a payload may be dropped on a row.</param>
    /// <param name="onDrop">Receives the row and payload after a successful drop.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void TreeView(this Gui gui, TreeViewState state, IReadOnlyList<TreeItem> items,
        TreeViewTheme? theme = null, Action<TreeViewEvent>? onClick = null,
        Func<TreeItem, object?>? dragPayload = null,
        Action<TreeItem, string>? onRename = null,
        Action<MouseButton>? onEmptyClick = null,
        Func<object, bool>? dropAccept = null,
        Action<TreeItem, object>? onDrop = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(items);

        theme ??= TreeViewTheme.Default;
        var visible = Flatten(items, state);

        using (gui.Node(filePath: filePath, lineNumber: lineNumber).Expand().Direction(Axis.Vertical).Enter())
        {
            gui.ScrollY();

            if (gui.Pass == Pass.Pass2Render) gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);

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
    }

    /// <summary>
    /// Drops the rows hidden under a collapsed parent. A collapsed row hides everything after it that
    /// is deeper, which is what makes a flat list enough to describe a tree.
    /// </summary>
    private static List<TreeItem> Flatten(IReadOnlyList<TreeItem> items, TreeViewState state)
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
    private static void Navigate(Gui gui, TreeViewState state, TreeViewTheme theme,
        List<TreeItem> visible, Action<TreeViewEvent>? onClick)
    {
        if (visible.Count == 0) return;

        var index = state.SelectedId is null
            ? -1
            : visible.FindIndex(item => item.Id == state.SelectedId);

        if (gui.Input.IsKeyPressed(KeyboardKey.Down)) Select(state, visible, Math.Min(index + 1, visible.Count - 1));
        else if (gui.Input.IsKeyPressed(KeyboardKey.Up)) Select(state, visible, Math.Max(index - 1, 0));
        else if (gui.Input.IsKeyPressed(KeyboardKey.Home)) Select(state, visible, 0);
        else if (gui.Input.IsKeyPressed(KeyboardKey.End)) Select(state, visible, visible.Count - 1);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Right)) Open(state, visible, index);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Left)) Close(state, visible, index);
        else if (gui.Input.IsKeyPressed(KeyboardKey.Enter) && index >= 0)
            onClick?.Invoke(new TreeViewEvent(visible[index], MouseButton.Left, 2));
        else return;

        ScrollToSelection(gui, state, theme, visible);
    }

    private static void Select(TreeViewState state, List<TreeItem> visible, int index)
    {
        if (index < 0 || index >= visible.Count) return;

        state.SelectedId = visible[index].Id;
    }

    private static void Open(TreeViewState state, List<TreeItem> visible, int index)
    {
        if (index < 0) return;

        var item = visible[index];
        if (item.HasChildren && state.IsCollapsed(item.Id, item.Depth)) state.SetExpanded(item.Id, true);
        else Select(state, visible, index + 1);
    }

    private static void Close(TreeViewState state, List<TreeItem> visible, int index)
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
    private static void ScrollToSelection(Gui gui, TreeViewState state, TreeViewTheme theme, List<TreeItem> visible)
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
    /// Records the viewport and scroll offset for the next frame to virtualise against.
    /// </summary>
    private static void Measure(Gui gui, TreeViewState state)
    {
        var rect = gui.CurrentNode.Rect;
        if (rect.H > 0) state.FrameViewportHeight = rect.H;

        state.FrameScrollY = gui.GetScrollState(gui.CurrentNode.Id) is { } scroll ? scroll.ScrollOffset.Y : 0f;
    }

    private static void Spacer(Gui gui, string id, float height)
    {
        if (height <= 0) return;

        using (gui.Node(-1, height, id).ExpandWidth().Enter())
        {
        }
    }

    private static void RenderRow(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item, int row,
        Action<TreeViewEvent>? onClick, Func<TreeItem, object?>? dragPayload,
        Func<object, bool>? dropAccept, Action<TreeItem, object>? onDrop,
        Action<TreeItem, string>? onRename)
    {
        var isSelected = item.Id == state.SelectedId;
        var isEditing = onRename is not null && state.EditingId == item.Id;

        using (gui.Node(-1, theme.RowHeight, $"treeview/row{row}")
                   .ExpandWidth()
                   .Direction(Axis.Horizontal)
                   .Padding((item.Depth * theme.IndentWidth) + theme.ContentPadding, 0)
                   .Gap(4f)
                   .Enter())
        {
            if (!isEditing && dragPayload?.Invoke(item) is { } payload)
                gui.DragSource($"treeview/row/{item.Id}", payload, ghost: g => DragGhost(g, theme, item));

            if (gui.Pass == Pass.Pass2Render)
            {
                if (dropAccept is not null)
                    gui.DropTarget($"treeview/drop/{item.Id}", dropAccept,
                        payload => onDrop?.Invoke(item, payload));

                var interactable = gui.GetInteractable();

                if (isSelected) gui.DrawBackgroundRect(theme.Selected, 2);
                else if (interactable.OnHover()) gui.DrawBackgroundRect(theme.Hover, 2);

                if (!isEditing)
                {
                    Report(state, item, interactable, MouseButton.Left, onClick);
                    Report(state, item, interactable, MouseButton.Right, onClick);
                    Report(state, item, interactable, MouseButton.Middle, onClick);
                }
            }

            Expander(gui, state, theme, item, row);

            if (item.Icon is { } icon)
                using (gui.Node(theme.IconSize, theme.RowHeight, $"treeview/row{row}/icon").Enter())
                    icon(gui);

            if (isEditing) RenameBox(gui, state, theme, item, onRename!);
            else
                gui.DrawText(item.Label, theme.FontSize,
                    item.Tint ?? (isSelected ? theme.Ink : theme.InkDim), centerInRect: false);
        }
    }

    /// <summary>
    /// The inline rename field. Enter commits, Escape abandons, and a click anywhere else commits too —
    /// the same bargain a file manager makes, so the box can never be left open by accident.
    /// </summary>
    private static void RenameBox(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item,
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
    private static void DragGhost(Gui gui, TreeViewTheme theme, TreeItem item)
    {
        using (gui.Node(-1, theme.RowHeight).Padding(6, 0).ContentAlignY(0.5f).Enter())
        {
            gui.DrawBackgroundRect(theme.Selected, 3);
            gui.DrawText(item.Label, theme.FontSize, theme.Ink, centerInRect: false);
        }
    }

    private static void Report(TreeViewState state, TreeItem item, InteractableElement interactable,
        MouseButton button, Action<TreeViewEvent>? onClick)
    {
        if (!interactable.OnClick(out var clicks, button)) return;

        if (button == MouseButton.Left)
        {
            state.SelectedId = item.Id;
            state.WantsFocus = true;

            // Single click selects, double click folds — the arrow is the one-click shortcut.
            if (clicks >= 2 && item.HasChildren) state.Toggle(item.Id, item.Depth);
        }

        onClick?.Invoke(new TreeViewEvent(item, button, clicks));
    }

    private static void Expander(Gui gui, TreeViewState state, TreeViewTheme theme, TreeItem item, int row)
    {
        // Blocks the row underneath, so clicking the arrow neither selects nor double-toggles.
        using (gui.Node(theme.ExpanderWidth, theme.RowHeight, $"treeview/row{row}/expander")
                   .BlockInput(item.HasChildren)
                   .Enter())
        {
            if (gui.Pass != Pass.Pass2Render || !item.HasChildren) return;

            var interactable = gui.GetInteractable();
            var rect = gui.CurrentNode.Rect;
            var centre = new Vector2(rect.X + (rect.W / 2f), rect.Y + (rect.H / 2f));

            DrawExpanderArrow(gui, centre, state.IsCollapsed(item.Id, item.Depth),
                interactable.OnHover() ? theme.Ink : theme.InkDim);

            if (interactable.OnClick()) state.Toggle(item.Id, item.Depth);
        }
    }

    private static void DrawExpanderArrow(Gui gui, Vector2 centre, bool collapsed, Color color)
    {
        const float size = 3.5f;

        if (collapsed)
            gui.DrawTriangleFilled(
                new Vector2(centre.X - size, centre.Y - size),
                new Vector2(centre.X - size, centre.Y + size),
                new Vector2(centre.X + size, centre.Y), color);
        else
            gui.DrawTriangleFilled(
                new Vector2(centre.X - size, centre.Y - size),
                new Vector2(centre.X + size, centre.Y - size),
                new Vector2(centre.X, centre.Y + size), color);
    }
}

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
    /// <param name="theme">Colours and metrics. Defaults to <see cref="TreeViewTheme.Default"/>.</param>
    /// <param name="onClick">Called for a click on a row, with the button and the click count.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void TreeView(this Gui gui, TreeViewState state, IReadOnlyList<TreeItem> items,
        TreeViewTheme? theme = null, Action<TreeViewEvent>? onClick = null,
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

            // Both passes must pick the same rows, and a node's rect is only resolved in the render
            // pass — so the window comes from what the previous frame measured.
            var first = Math.Max(0, (int)(state.FrameScrollY / theme.RowHeight) - Overscan);
            var take = (int)(state.FrameViewportHeight / theme.RowHeight) + (Overscan * 2) + 1;
            var last = Math.Min(visible.Count, first + take);

            Spacer(gui, "treeview/padTop", first * theme.RowHeight);

            for (var i = first; i < last; i++)
                RenderRow(gui, state, theme, visible[i], i, onClick);

            Spacer(gui, "treeview/padBottom", (visible.Count - last) * theme.RowHeight);

            if (gui.Pass == Pass.Pass2Render) Measure(gui, state);
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
        Action<TreeViewEvent>? onClick)
    {
        var isSelected = item.Id == state.SelectedId;

        using (gui.Node(-1, theme.RowHeight, $"treeview/row{row}")
                   .ExpandWidth()
                   .Direction(Axis.Horizontal)
                   .Padding((item.Depth * theme.IndentWidth) + 4f, 0)
                   .Gap(4f)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var interactable = gui.GetInteractable();

                if (isSelected) gui.DrawBackgroundRect(theme.Selected, 2);
                else if (interactable.OnHover()) gui.DrawBackgroundRect(theme.Hover, 2);

                Report(gui, state, item, interactable, MouseButton.Left, onClick);
                Report(gui, state, item, interactable, MouseButton.Right, onClick);
                Report(gui, state, item, interactable, MouseButton.Middle, onClick);
            }

            Expander(gui, state, theme, item, row);

            if (item.Icon is { } icon)
                using (gui.Node(theme.IconSize, theme.RowHeight, $"treeview/row{row}/icon").Enter())
                    icon(gui);

            gui.DrawText(item.Label, theme.FontSize,
                item.Tint ?? (isSelected ? theme.Ink : theme.InkDim), centerInRect: false);
        }
    }

    private static void Report(Gui gui, TreeViewState state, TreeItem item, InteractableElement interactable,
        MouseButton button, Action<TreeViewEvent>? onClick)
    {
        if (!interactable.OnClick(out var clicks, button)) return;

        if (button == MouseButton.Left)
        {
            state.SelectedId = item.Id;

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

using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private const int MenuBarZIndex = 5000;
    internal const int CascadeMenuZIndex = 9000;

    /// <summary>
    /// The bar's state, in two halves. Input arrives during the render pass and writes the live half;
    /// what a frame actually draws is the <c>Frame</c> half, sampled once at the start of the build
    /// pass. Without that split, opening a menu in the render pass creates a dropdown the layout pass
    /// never saw, and every one of its labels draws at the window's origin for a frame.
    /// </summary>
    private class MenuBarState
    {
        public int OpenIndex { get; set; } = -1;
        public bool KeyboardActive { get; set; }
        public int KeyboardIndex { get; set; } = -1;
        public bool KeyboardSubmenu { get; set; }

        public int FrameOpenIndex { get; set; } = -1;
        public bool FrameKeyboardActive { get; set; }
        public int FrameKeyboardIndex { get; set; } = -1;
        public bool FrameKeyboardSubmenu { get; set; }

        public List<Rect> TitleRects { get; set; } = new();
        public List<Rect> FrameTitleRects { get; set; } = new();
        public List<Rect> SubmenuRects { get; set; } = new();
        public List<Rect> PrevSubmenuRects { get; set; } = new();

        /// <summary>Samples the live state into the half this frame draws from.</summary>
        public void BeginFrame()
        {
            FrameOpenIndex = OpenIndex;
            FrameKeyboardActive = KeyboardActive;
            FrameKeyboardIndex = KeyboardIndex;
            FrameKeyboardSubmenu = KeyboardSubmenu;
            FrameTitleRects = [.. TitleRects];
        }
    }

    /// <summary>
    /// Creates a menu bar. Title clicks drop a menu below the title; submenus, separators, disabled
    /// items, shortcut labels and checkable items are supported. Clicking outside (or pressing Escape)
    /// dismisses the open menu.
    /// </summary>
    public static void MenuBar(this Gui gui, Action<MenuBarBuilder> buildMenus,
        float height = 30,
        Color? backgroundColor = null,
        Color? textColor = null,
        Color? hoverColor = null,
        float fontSize = 12,
        float padding = 12,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(buildMenus);

        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new MenuBarState());

        var builder = new MenuBarBuilder();
        buildMenus(builder);

        if (state.OpenIndex >= builder.Menus.Count) state.OpenIndex = -1;

        // Submenu cascade rects from the previous frame preserve chain stability while the pointer
        // travels inside them. Rotate exactly once per frame so both passes agree.
        if (gui.Pass == Pass.Pass1Build)
        {
            state.PrevSubmenuRects = state.SubmenuRects;
            state.SubmenuRects = new List<Rect>();
            state.BeginFrame();
        }

        using (gui.Node().Height(height).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawBackgroundRect(backgroundColor ?? gui.Controls.Surface);

                // A rule under the bar, not a box around it: the bar spans its host's width, so an
                // outline on the other three edges reads as a stray rectangle rather than chrome.
                var bar = gui.CurrentNode.Rect;
                gui.DrawRect(new Rect(bar.X, bar.Y + bar.H - 1, bar.W, 1), gui.Controls.Border);
            }

            if (state.TitleRects.Count != builder.Menus.Count)
                state.TitleRects = builder.Menus.Select(_ => new Rect()).ToList();

            for (var i = 0; i < builder.Menus.Count; i++)
                RenderMenuBarTitle(gui, state, builder.Menus[i], i, height, textColor, hoverColor, fontSize,
                    padding);
        }

        if (state.FrameOpenIndex >= 0 && gui.Pass == Pass.Pass2Render)
            HandleMenuKeyboard(gui, state, builder.Menus[state.FrameOpenIndex].Items);

        if (state.FrameOpenIndex >= 0)
            RenderMenuBarDropdown(gui, state, builder.Menus[state.FrameOpenIndex], height,
                backgroundColor, textColor, hoverColor, fontSize, padding);

        // Clicking anywhere that is neither a title nor the open menu cascade dismisses the menu.
        if (gui.Pass == Pass.Pass2Render && state.FrameOpenIndex >= 0 &&
            gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = gui.Input.MousePosition;
            var insideTitle = state.TitleRects.Any(r => IsMouseInRect(mousePos, r));
            var insideMenu = state.SubmenuRects.Any(r => IsMouseInRect(mousePos, r));
            if (!insideTitle && !insideMenu)
                ResetMenuState(state);
        }
    }

    private static void RenderMenuBarTitle(Gui gui, MenuBarState state, MenuBarMenu menu, int index,
        float height, Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        using (gui.Node().Height(height).Padding(padding, 0).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                state.TitleRects[index] = rect;

                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                var isClicked = interactable.OnClick();

                var isOpen = state.FrameOpenIndex == index;

                if (isClicked ||
                    (gui.HasFocus() &&
                     (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter))))
                {
                    if (isOpen)
                    {
                        ResetMenuState(state);
                    }
                    else
                    {
                        state.OpenIndex = index;
                        state.KeyboardActive = false;
                        state.KeyboardIndex = -1;
                        state.KeyboardSubmenu = false;
                    }
                }
                else if (state.FrameOpenIndex >= 0 && !isOpen && isHovered)
                {
                    state.OpenIndex = index;
                    state.KeyboardActive = false;
                    state.KeyboardIndex = -1;
                    state.KeyboardSubmenu = false;
                }

                if (isOpen || isHovered)
                    gui.DrawBackgroundRect(hoverColor ?? gui.Controls.SurfaceHover, 2);
            }

            // Built in both passes so the text node is measured during layout, not created after it.
            gui.DrawText(menu.Title, fontSize, textColor, centerInRect: false);
        }
    }

    private static void RenderMenuBarDropdown(Gui gui, MenuBarState state, MenuBarMenu menu, float height,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        if (state.FrameOpenIndex >= state.FrameTitleRects.Count) return;

        var anchor = state.FrameTitleRects[state.FrameOpenIndex];
        if (anchor.W <= 0 || anchor.H <= 0) return;

        RenderMenuGroup(gui, state, menu.Title, menu.Items,
            new Vector2(anchor.X, anchor.Y + height), depth: 0,
            backgroundColor, textColor, hoverColor, fontSize, padding);
    }

    private static void RenderMenuGroup(Gui gui, MenuBarState state, string baseId, List<FlyoutItem> items,
        Vector2 position, int depth,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding)
        => RenderMenuGroup(gui, state, baseId, items, position, depth, backgroundColor, textColor,
            hoverColor, fontSize, padding, MenuBarZIndex);

    private static void RenderMenuGroup(Gui gui, MenuBarState state, string baseId, List<FlyoutItem> items,
        Vector2 position, int depth,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding, int zIndex)
    {
        const float itemHeight = 26f;
        const float separatorHeight = 9f;

        var hasCheckColumn = items.Any(i => i.IsChecked != null);
        var menuWidth = CalculateFlyoutWidth(items, fontSize, padding, minWidth: 160, hasCheckColumn);
        var menuHeight = items.Sum(i => i.IsSeparator ? separatorHeight : itemHeight);

        var adjustedPosition = ConstrainToScreen(gui, position, menuWidth, menuHeight);
        var groupRect = new Rect(adjustedPosition.X, adjustedPosition.Y, menuWidth, menuHeight);

        state.SubmenuRects.Add(groupRect);

        var mousePos = gui.Input.MousePosition;
        var hoverIndex = HoveredRowIndex(items, groupRect, mousePos, itemHeight, separatorHeight);

        // A row keeps its submenu open while the pointer is either on it, on the keyboard-selected
        // row, or anywhere inside a submenu that was open last frame (cascade stability).
        var openSubmenuIndex = -1;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!item.HasSubmenu || !item.Enabled) continue;
            var rowY = groupRect.Y + RowOffset(items, i, itemHeight, separatorHeight);
            var shouldOpen = hoverIndex == i ||
                             (state.FrameKeyboardActive && state.FrameKeyboardIndex == i
                                                        && state.FrameKeyboardSubmenu) ||
                             state.PrevSubmenuRects.Any(r => IsMouseInRect(mousePos, r)
                                 && IsSubmenuForRow(r, groupRect, rowY, itemHeight));
            if (shouldOpen)
            {
                openSubmenuIndex = i;
                break;
            }
        }

        var nodeId = $"/menubar/{baseId}/v{depth}";
        using (gui.Node(menuWidth, menuHeight, nodeId)
                   .AbsoluteScreen(adjustedPosition.X, adjustedPosition.Y)
                   .BlockInput()
                   .Enter())
        {
            gui.SetZIndex(zIndex);
            gui.SetEscapesAncestorClips();

            if (gui.Pass == Pass.Pass2Render)
            {
                gui.DrawBackgroundRect(backgroundColor ?? gui.Controls.Popup, 4);
                gui.DrawRectBorder(gui.CurrentNode.Rect, gui.Controls.Border, 1f, 4);
            }

            for (var i = 0; i < items.Count; i++)
                RenderMenuBarRow(gui, state, items, i, nodeId, menuWidth, itemHeight, separatorHeight,
                    hasCheckColumn, textColor, hoverColor, fontSize, padding);
        }

        if (openSubmenuIndex >= 0 && items[openSubmenuIndex].Submenu is { } submenu)
        {
            var rowOffset = RowOffset(items, openSubmenuIndex, itemHeight, separatorHeight);
            RenderMenuGroup(gui, state, $"{baseId}/{items[openSubmenuIndex].Text}", submenu,
                new Vector2(groupRect.X + groupRect.W, groupRect.Y + rowOffset), depth + 1,
                backgroundColor, textColor, hoverColor, fontSize, padding, zIndex);
        }
    }

    private static bool IsSubmenuForRow(Rect submenu, Rect parent, float rowY, float rowHeight)
    {
        var rowCenter = rowY + rowHeight * 0.5f;
        var attachedToParent = submenu.X >= parent.X + parent.W - 1
            || submenu.X + submenu.W <= parent.X + 1;
        return attachedToParent && submenu.Y <= rowCenter && submenu.Y + submenu.H >= rowCenter;
    }

    private static void RenderMenuBarRow(Gui gui, MenuBarState state, List<FlyoutItem> items, int index,
        string nodeId, float width, float itemHeight, float separatorHeight,
        bool hasCheckColumn, Color? textColor, Color? hoverColor,
        float fontSize, float padding)
    {
        var item = items[index];

        if (item.IsSeparator)
        {
            using (gui.Node(width, separatorHeight, $"{nodeId}/s{index}").Enter())
            {
                if (gui.Pass != Pass.Pass2Render) return;

                var rect = gui.CurrentNode.Rect;
                var sepColor = gui.Controls.Border;
                var sepY = rect.Y + rect.H * 0.5f;
                gui.DrawLine(new Vector2(rect.X + padding, sepY),
                    new Vector2(rect.X + rect.W - padding, sepY), sepColor);
            }
            return;
        }

        using (gui.Node(width, itemHeight, $"{nodeId}/i{index}")
                   .Padding(padding, 0)
                   .Direction(Axis.Horizontal)
                   .ContentAlignY(0.5f)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
                var interactable = gui.GetInteractable();
                var isHovered = interactable.OnHover();
                if (isHovered && item.Enabled) item.OnHover?.Invoke();
                if (interactable.OnClick() && item.Enabled)
                    ActivateRow(state, item);

                var isSelectedRow = isHovered ||
                                    (state.FrameKeyboardActive && state.FrameKeyboardIndex == index);

                if (isSelectedRow && item.Enabled)
                    gui.DrawBackgroundRect(hoverColor ?? gui.Controls.SurfaceHover, 2);
            }

            var itemColor = item.Enabled ? textColor ?? gui.Controls.Text : gui.Controls.TextDim;

            // Rows and their glyphs are built in both passes so they measure during layout.
            if (hasCheckColumn)
            {
                using (gui.Node(14f).Enter())
                    if (gui.Pass == Pass.Pass2Render && item.IsChecked?.Invoke() == true)
                        DrawTick(gui, gui.CurrentNode.Rect, itemColor);
            }

            gui.DrawText(item.Text, fontSize, itemColor, centerInRect: false);

            gui.Node().Expand();

            if (item.HasSubmenu)
            {
                using (gui.Node(fontSize).Enter())
                    if (gui.Pass == Pass.Pass2Render)
                        DrawSubmenuArrow(gui, gui.CurrentNode.Rect, itemColor);
            }
            else if (!string.IsNullOrEmpty(item.Shortcut))
                gui.DrawText(item.Shortcut, fontSize * 0.9f, gui.Controls.TextDim, centerInRect: false);
        }
    }

    /// <summary>
    /// The tick beside a checked item, drawn rather than typed. A host's font often has no U+2713, and
    /// a missing glyph renders as a blank box in the middle of the menu.
    /// </summary>
    private static void DrawTick(Gui gui, Rect rect, Color color)
    {
        var x = rect.X + (rect.W * 0.22f);
        var y = rect.Y + (rect.H * 0.52f);
        var mid = new Vector2(rect.X + (rect.W * 0.42f), rect.Y + (rect.H * 0.72f));

        gui.DrawLine(new Vector2(x, y), mid, color, 1.6f);
        gui.DrawLine(mid, new Vector2(rect.X + (rect.W * 0.80f), rect.Y + (rect.H * 0.28f)), color, 1.6f);
    }

    /// <summary>The right-pointing arrow marking a submenu, drawn for the same reason as the tick.</summary>
    private static void DrawSubmenuArrow(Gui gui, Rect rect, Color color)
    {
        var size = Math.Min(rect.W, rect.H) * 0.42f;
        var cx = rect.X + (rect.W * 0.5f);
        var cy = rect.Y + (rect.H * 0.5f);

        gui.DrawTriangleFilled(
            new Vector2(cx - (size * 0.5f), cy - size),
            new Vector2(cx - (size * 0.5f), cy + size),
            new Vector2(cx + (size * 0.8f), cy),
            color);
    }

    private static void HandleMenuKeyboard(Gui gui, MenuBarState state, List<FlyoutItem> items)
    {
        if (gui.Input.IsKeyPressed(KeyboardKey.Escape))
        {
            ResetMenuState(state);
            return;
        }

        var isDown = gui.Input.IsKeyPressed(KeyboardKey.Down);
        var isUp = gui.Input.IsKeyPressed(KeyboardKey.Up);

        if (isDown || isUp)
        {
            if (!state.KeyboardActive)
            {
                state.KeyboardActive = true;
                state.KeyboardIndex = NextSelectableIndex(items, -1, isDown ? 1 : -1);
                state.KeyboardSubmenu = false;
            }
            else
            {
                state.KeyboardIndex = NextSelectableIndex(items, state.KeyboardIndex, isDown ? 1 : -1);
            }
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Right) && state.KeyboardIndex >= 0 &&
            items[state.KeyboardIndex].HasSubmenu)
        {
            state.KeyboardSubmenu = true;
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.Left))
        {
            state.KeyboardSubmenu = false;
        }

        if (state.KeyboardActive &&
            (gui.Input.IsKeyPressed(KeyboardKey.Enter) || gui.Input.IsKeyPressed(KeyboardKey.Space)))
        {
            if (state.KeyboardIndex >= 0 && state.KeyboardIndex < items.Count)
                ActivateRow(state, items[state.KeyboardIndex]);
        }
    }

    private static void ActivateRow(MenuBarState state, FlyoutItem item)
    {
        if (!item.Enabled) return;

        if (item.HasSubmenu)
        {
            state.KeyboardSubmenu = true;
            return;
        }

        if (item.IsChecked is not null && item.OnCheckChanged is not null)
            item.OnCheckChanged(!item.IsChecked());
        else
            item.Action?.Invoke();

        ResetMenuState(state);
    }

    private static void ResetMenuState(MenuBarState state)
    {
        state.OpenIndex = -1;
        state.KeyboardActive = false;
        state.KeyboardIndex = -1;
        state.KeyboardSubmenu = false;
    }

    private static int HoveredRowIndex(List<FlyoutItem> items, Rect groupRect, Vector2 mousePos,
        float itemHeight, float separatorHeight)
    {
        if (!IsMouseInRect(mousePos, groupRect)) return -1;

        var relY = mousePos.Y - groupRect.Y;
        var accY = 0f;
        for (var i = 0; i < items.Count; i++)
        {
            var h = items[i].IsSeparator ? separatorHeight : itemHeight;
            if (relY >= accY && relY < accY + h) return i;
            accY += h;
        }

        return -1;
    }

    private static int NextSelectableIndex(List<FlyoutItem> items, int from, int delta)
    {
        var count = items.Count;
        for (var step = 1; step <= count; step++)
        {
            var index = (from + delta * step) % count;
            if (index < 0) index += count;
            if (!items[index].IsSeparator) return index;
        }

        return -1;
    }

    private static float RowOffset(List<FlyoutItem> items, int index, float itemHeight, float separatorHeight)
    {
        var y = 0f;
        for (var i = 0; i < index; i++)
            y += items[i].IsSeparator ? separatorHeight : itemHeight;
        return y;
    }
}

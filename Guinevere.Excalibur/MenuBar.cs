using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    const int MenuBarZIndex = 5000;
    internal const int CascadeMenuZIndex = 9000;

    /// <summary>
    /// Keeps live input state separate from frame snapshots so build and render use the same menu tree.
    /// Branches and check marks are selected once during build.
    /// </summary>
    class MenuBarState
    {
        public int OpenIndex { get; set; } = -1;
        public bool KeyboardActive { get; set; }

        public int FrameOpenIndex { get; set; } = -1;
        public bool FrameKeyboardActive { get; set; }
        public bool Expanded { get; set; }
        public bool FrameExpanded { get; set; }
        public List<int> KeyboardPath { get; set; } = [];
        public List<int> FrameKeyboardPath { get; set; } = [];
        public Dictionary<string, MenuBranch> Branches { get; } = [];
        public Dictionary<string, int> FrameBranches { get; } = [];
        public Dictionary<string, bool> FrameChecks { get; } = [];
        public MenuAppearance Appearance { get; set; } = new();
        public Rect BarRect { get; set; }
        public Rect FrameBarRect { get; set; }
        public Vector2 PreviousMouse { get; set; }

        public List<Rect> TitleRects { get; set; } = [];
        public List<Rect> FrameTitleRects { get; set; } = [];
        public List<Rect> SubmenuRects { get; set; } = [];

        /// <summary>Snapshots the live menu state for both passes.</summary>
        public void BeginFrame(Vector2 mouse)
        {
            if (mouse != PreviousMouse) KeyboardActive = false;
            PreviousMouse = mouse;
            FrameOpenIndex = OpenIndex;
            FrameKeyboardActive = KeyboardActive;
            FrameExpanded = Expanded;
            FrameKeyboardPath = [.. KeyboardPath];
            FrameTitleRects = [.. TitleRects];
            FrameBarRect = BarRect;
            FrameBranches.Clear();
            FrameChecks.Clear();
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
        float fontSize = ControlMetrics.CompactFontSize,
        float padding = ControlMetrics.ComfortableSpacing,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.CompactFontSizeOr(fontSize);
        padding = gui.ControlStyle.ComfortableSpacingOr(padding);

        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(buildMenus);

        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new MenuBarState());

        var builder = new MenuBarBuilder();
        buildMenus(builder);

        if (state.OpenIndex >= builder.Menus.Count) state.OpenIndex = -1;

        if (gui.Pass == Pass.Pass1Build)
        {
            state.SubmenuRects = [];
            state.BeginFrame(gui.Input.MousePosition);
        }

        RenderMenuBar(gui, state, builder, height, backgroundColor, textColor, hoverColor, fontSize, padding);
        RenderOpenMenuBar(gui, state, builder, id, height, backgroundColor, textColor, hoverColor, fontSize, padding);
        DismissMenuBarOutside(gui, state);
    }

    static void RenderMenuBar(Gui gui, MenuBarState state, MenuBarBuilder builder, float height,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        var barNode = gui.Node().Height(height).Direction(Axis.Horizontal);
        if (builder.CollapsedLabel is not null) barNode.Width(UnitValue.Fit);
        using var scope = barNode.Enter();
        if (gui.Pass == Pass.Pass2Render)
        {
            gui.DrawBackgroundRect(backgroundColor ?? gui.ControlStyle.Surface);
            var bar = gui.CurrentNode.Rect;
            state.BarRect = bar;
            gui.DrawRect(new Rect(bar.X, bar.Y + bar.H - 1, bar.W, 1), gui.ControlStyle.Border);
        }
        RenderMenuBarTitles(gui, state, builder, height, textColor, hoverColor, fontSize, padding);
    }

    static void RenderMenuBarTitles(Gui gui, MenuBarState state, MenuBarBuilder builder, float height,
        Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        if (state.TitleRects.Count != builder.Menus.Count)
            state.TitleRects = [.. builder.Menus.Select(_ => new Rect())];
        if (builder.CollapsedLabel is not null && !state.FrameExpanded)
        {
            if (RenderCompactMenuToggle(gui, builder.CollapsedLabel, height) && builder.Menus.Count > 0)
            {
                state.Expanded = true;
                OpenMenuBarTitle(state, 0);
            }
        }
        else
            for (var i = 0; i < builder.Menus.Count; i++)
                RenderMenuBarTitle(gui, state, builder.Menus[i], i, height, textColor, hoverColor, fontSize, padding);
    }

    static bool RenderCompactMenuToggle(Gui gui, string label, float height)
    {
        using var scope = gui.Node(height, height).Enter();
        var hamburger = label == "☰";
        var activated = gui.Button(hamburger ? "" : label, height, height);
        if (hamburger)
        {
            using var glyph = gui.Node(height, height).Absolute(0, 0).HitTestVisible(false).Enter();
            if (gui.Pass != Pass.Pass2Render) return activated;
            var rect = gui.CurrentNode.Rect;
            var x = rect.X + (rect.W - 12) * 0.5f;
            var y = rect.Y + rect.H * 0.5f;
            for (var offset = -4; offset <= 4; offset += 4)
                gui.DrawLine(new Vector2(x, y + offset), new Vector2(x + 12, y + offset), gui.ControlStyle.Text);
        }
        return activated;
    }

    static void RenderOpenMenuBar(Gui gui, MenuBarState state, MenuBarBuilder builder, string id, float height,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        if (state.FrameOpenIndex < 0) return;
        var menu = builder.Menus[state.FrameOpenIndex];
        if (gui.Pass == Pass.Pass2Render) HandleMenuKeyboard(gui, state, menu.Items);
        using var focusScope = gui.EnterFocusNavigationScope($"{id}/focus");
        focusScope.SetActive();
        if (builder.CollapsedLabel is not null)
            RenderMenuGroup(gui, state, menu.Title, menu.Items,
                new Vector2(state.FrameBarRect.X, state.FrameBarRect.Y + height), 0,
                backgroundColor, textColor, hoverColor, fontSize, padding);
        else
            RenderMenuBarDropdown(gui, state, menu, height,
                backgroundColor, textColor, hoverColor, fontSize, padding);
    }

    static void DismissMenuBarOutside(Gui gui, MenuBarState state)
    {
        if (gui.Pass != Pass.Pass2Render || state.FrameOpenIndex < 0
            || !gui.Input.IsMouseButtonPressed(MouseButton.Left)) return;
        var mouse = gui.Input.MousePosition;
        if (!state.TitleRects.Any(rect => rect.Contains(mouse))
            && !state.SubmenuRects.Any(rect => rect.Contains(mouse)))
            ResetMenuState(state);
    }

    static void RenderMenuBarTitle(Gui gui, MenuBarState state, MenuBarMenu menu, int index,
        float height, Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        using var scope = gui.Node().Height(height).Padding(padding, 0).ContentAlignY(0.5f).Enter();
        if (gui.Pass == Pass.Pass2Render)
        {
            state.TitleRects[index] = gui.CurrentNode.Rect;
            gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
            var interaction = gui.GetInteractable();
            var hovered = interaction.OnHover();
            var clicked = interaction.OnClick();
            if (clicked) gui.RequestFocus(FocusReason.Mouse);
            UpdateMenuBarTitle(state, index, hovered, clicked || MenuKeyboardActivated(gui));
            if (state.FrameOpenIndex == index || hovered)
                gui.DrawBackgroundRect(hoverColor ?? gui.ControlStyle.SurfaceHover, 2);
        }
        gui.DrawText(menu.Title, fontSize, textColor ?? gui.ControlStyle.Text, centerInRect: false);
    }

    static bool MenuKeyboardActivated(Gui gui) => gui.HasFocus()
        && (gui.Input.IsKeyPressed(KeyboardKey.Space) || gui.Input.IsKeyPressed(KeyboardKey.Enter));

    static void UpdateMenuBarTitle(MenuBarState state, int index, bool hovered, bool activated)
    {
        var open = state.FrameOpenIndex == index;
        if (activated)
        {
            if (open) ResetMenuState(state);
            else OpenMenuBarTitle(state, index);
        }
        else if (state.FrameOpenIndex >= 0 && !open && hovered)
            OpenMenuBarTitle(state, index);
    }

    static void OpenMenuBarTitle(MenuBarState state, int index)
    {
        state.OpenIndex = index;
        state.Branches.Clear();
        state.KeyboardPath.Clear();
        state.KeyboardActive = false;
    }

    static void RenderMenuBarDropdown(Gui gui, MenuBarState state, MenuBarMenu menu, float height,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding)
    {
        if (state.FrameOpenIndex >= state.FrameTitleRects.Count) return;

        var anchor = state.FrameTitleRects[state.FrameOpenIndex];
        if (anchor.W <= 0 || anchor.H <= 0) return;

        RenderMenuGroup(gui, state, menu.Title, menu.Items,
            new Vector2(anchor.X, anchor.Y + height), depth: 0,
            backgroundColor, textColor, hoverColor, fontSize, padding);
    }

    static void RenderMenuGroup(Gui gui, MenuBarState state, string baseId, List<FlyoutItem> items,
        Vector2 position, int depth,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding)
        => RenderMenuGroup(gui, state, baseId, items, position, depth, backgroundColor, textColor,
            hoverColor, fontSize, padding, MenuBarZIndex);

    static void RenderMenuGroup(Gui gui, MenuBarState state, string baseId, List<FlyoutItem> items,
        Vector2 position, int depth,
        Color? backgroundColor, Color? textColor, Color? hoverColor, float fontSize, float padding, int zIndex)
    {
        var appearance = state.Appearance;
        var itemHeight = appearance.ItemHeight;
        var separatorHeight = appearance.SeparatorHeight;

        var hasCheckColumn = items.Any(i => i.IsChecked != null);
        var menuWidth = CalculateFlyoutWidth(items, fontSize, padding, appearance.MinWidth, hasCheckColumn);
        var menuHeight = items.Sum(i => i.IsSeparator ? separatorHeight : itemHeight);

        var adjustedPosition = ConstrainToScreen(gui, position, menuWidth, menuHeight);
        var groupRect = new Rect(adjustedPosition.X, adjustedPosition.Y, menuWidth, menuHeight);

        state.SubmenuRects.Add(groupRect);

        var mousePos = gui.Input.MousePosition;
        var hoverIndex = HoveredRowIndex(items, groupRect, mousePos, itemHeight, separatorHeight);

        var openSubmenuIndex = ResolveMenuBranch(gui, state, baseId, items, groupRect, hoverIndex, depth);

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
                gui.DrawBackgroundRect(backgroundColor ?? gui.ControlStyle.Popup, appearance.Radius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, appearance.Border ?? gui.ControlStyle.Border, 1f,
                    appearance.Radius);
            }

            for (var i = 0; i < items.Count; i++)
                RenderMenuBarRow(gui, state, items, i, nodeId, menuWidth, itemHeight, separatorHeight,
                    hasCheckColumn, textColor, hoverColor, fontSize, padding, depth);
        }

        if (openSubmenuIndex >= 0 && items[openSubmenuIndex].Submenu is { } submenu)
        {
            var rowOffset = RowOffset(items, openSubmenuIndex, itemHeight, separatorHeight);
            RenderMenuGroup(gui, state, $"{baseId}/{items[openSubmenuIndex].Text}", submenu,
                SubmenuPosition(gui, submenu, groupRect, rowOffset, fontSize, padding, appearance.MinWidth), depth + 1,
                backgroundColor, textColor, hoverColor, fontSize, padding, zIndex);
        }
    }

    static void RenderMenuBarRow(Gui gui, MenuBarState state, List<FlyoutItem> items, int index,
        string nodeId, float width, float itemHeight, float separatorHeight,
        bool hasCheckColumn, Color? textColor, Color? hoverColor, float fontSize, float padding, int depth)
    {
        var item = items[index];
        if (item.IsSeparator)
        {
            RenderMenuSeparator(gui, state, width, separatorHeight, $"{nodeId}/s{index}", padding);
            return;
        }
        using var scope = gui.Node(width, itemHeight, $"{nodeId}/i{index}")
            .Padding(padding, 0).Direction(Axis.Horizontal).ContentAlignY(0.5f).Enter();
        if (gui.Pass == Pass.Pass2Render) HandleMenuRow(gui, state, item, index, depth, hoverColor);
        RenderMenuRowContent(gui, state, item, hasCheckColumn, textColor, fontSize);
    }

    static void RenderMenuSeparator(Gui gui, MenuBarState state, float width, float height, string id, float padding)
    {
        using var scope = gui.Node(width, height, id).Enter();
        if (gui.Pass != Pass.Pass2Render) return;
        var rect = gui.CurrentNode.Rect;
        var color = state.Appearance.Separator ?? gui.ControlStyle.Border;
        var y = rect.Y + rect.H * 0.5f;
        gui.DrawLine(new Vector2(rect.X + padding, y), new Vector2(rect.X + rect.W - padding, y), color);
    }

    static void HandleMenuRow(Gui gui, MenuBarState state, FlyoutItem item, int index, int depth, Color? hoverColor)
    {
        gui.RegisterFocusable(canReceiveFocus: item.Enabled, isInteractable: true);
        var interaction = gui.GetInteractable();
        var hovered = interaction.OnHover();
        if (hovered) PreviewMenuRow(item);
        if (interaction.OnClick()) ActivateMenuRow(state, item, depth, index);
        if (item.Enabled && MenuRowSelected(state, depth, index, hovered))
            gui.DrawBackgroundRect(hoverColor ?? gui.ControlStyle.SurfaceHover, 2);
    }

    static void PreviewMenuRow(FlyoutItem item)
    {
        if (item.Enabled) item.OnHover?.Invoke();
    }

    static bool MenuRowSelected(MenuBarState state, int depth, int index, bool hovered) => hovered
        || (state.FrameKeyboardActive && state.FrameKeyboardPath.Count > depth
            && state.FrameKeyboardPath[depth] == index);

    static void RenderMenuRowContent(Gui gui, MenuBarState state, FlyoutItem item, bool hasCheckColumn,
        Color? textColor, float fontSize)
    {
        var color = MenuItemColor(gui, state, item, textColor);
        RenderMenuCheckColumn(gui, state, item, hasCheckColumn, fontSize, color);
        gui.DrawText(item.Text, fontSize, color, centerInRect: false);
        gui.Node().Expand();
        if (item.HasSubmenu)
        {
            using var scope = gui.Node(fontSize).ContentAlignX(0.5f).ContentAlignY(0.5f).Enter();
            gui.DrawText(WidgetIcons.ChevronRight, fontSize * 0.7f, color);
        }
        else if (!string.IsNullOrEmpty(item.Shortcut))
            gui.DrawText(item.Shortcut, fontSize * 0.9f, gui.ControlStyle.TextDim, centerInRect: false);
    }

    static Color MenuItemColor(Gui gui, MenuBarState state, FlyoutItem item, Color? textColor) =>
        item.Enabled ? textColor ?? gui.ControlStyle.Text : state.Appearance.Disabled ?? gui.ControlStyle.TextDim;

    static void RenderMenuCheckColumn(Gui gui, MenuBarState state, FlyoutItem item, bool hasCheckColumn,
        float fontSize, Color color)
    {
        if (!hasCheckColumn) return;
        using var scope = gui.Node(14f).ContentAlignX(0.5f).ContentAlignY(0.5f).Enter();
        var id = gui.CurrentNode.Id;
        if (gui.Pass == Pass.Pass1Build) state.FrameChecks[id] = item.IsChecked?.Invoke() == true;
        if (state.FrameChecks.GetValueOrDefault(id)) gui.DrawText(WidgetIcons.Check, fontSize, color);
    }

    static void HandleMenuKeyboard(Gui gui, MenuBarState state, List<FlyoutItem> root)
    {
        if (gui.Input.IsKeyPressed(KeyboardKey.Escape))
        {
            ResetMenuState(state);
            return;
        }

        var items = KeyboardMenu(state, root);
        StepMenuKeyboard(gui, state, items);
        if (state.KeyboardPath.Count == 0) return;
        var index = state.KeyboardPath[^1];
        if (index < 0 || index >= items.Count) return;
        HandleMenuKeyboardBranch(gui, state, items, index);
    }

    static void StepMenuKeyboard(Gui gui, MenuBarState state, List<FlyoutItem> items)
    {
        var down = gui.Input.IsKeyPressed(KeyboardKey.Down);
        var up = gui.Input.IsKeyPressed(KeyboardKey.Up);
        if (down || up)
        {
            state.KeyboardActive = true;
            if (state.KeyboardPath.Count == 0) state.KeyboardPath.Add(-1);
            state.KeyboardPath[^1] = NextSelectableIndex(items, state.KeyboardPath[^1], down ? 1 : -1);
        }
    }

    static void HandleMenuKeyboardBranch(Gui gui, MenuBarState state, List<FlyoutItem> items, int index)
    {
        if (gui.Input.IsKeyPressed(KeyboardKey.Right) && items[index].HasSubmenu)
        {
            state.KeyboardActive = true;
            state.KeyboardPath.Add(NextSelectableIndex(items[index].Submenu!, -1, 1));
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.Left) && state.KeyboardPath.Count > 1)
            state.KeyboardPath.RemoveAt(state.KeyboardPath.Count - 1);
        else if (state.KeyboardActive
                 && (gui.Input.IsKeyPressed(KeyboardKey.Enter) || gui.Input.IsKeyPressed(KeyboardKey.Space)))
            ActivateMenuRow(state, items[index], state.KeyboardPath.Count - 1, index);
    }

    static void ActivateRow(MenuBarState state, FlyoutItem item)
    {
        if (!item.Enabled) return;

        if (item.IsChecked is not null && item.OnCheckChanged is not null)
            item.OnCheckChanged(!item.IsChecked());
        else
            item.Action?.Invoke();

        ResetMenuState(state);
    }

    static void ResetMenuState(MenuBarState state)
    {
        state.OpenIndex = -1;
        state.KeyboardActive = false;
        state.Expanded = false;
        state.KeyboardPath.Clear();
        state.Branches.Clear();
    }

    static int HoveredRowIndex(List<FlyoutItem> items, Rect groupRect, Vector2 mousePos,
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

    static int NextSelectableIndex(List<FlyoutItem> items, int from, int delta)
    {
        var count = items.Count;
        for (var step = 1; step <= count; step++)
        {
            var index = (from + delta * step) % count;
            if (index < 0) index += count;
            if (!items[index].IsSeparator && items[index].Enabled) return index;
        }

        return -1;
    }

    static float RowOffset(List<FlyoutItem> items, int index, float itemHeight, float separatorHeight)
    {
        var y = 0f;
        for (var i = 0; i < index; i++)
            y += items[i].IsSeparator ? separatorHeight : itemHeight;
        return y;
    }
}

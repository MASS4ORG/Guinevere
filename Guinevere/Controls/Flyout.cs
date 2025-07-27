using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private static readonly Dictionary<string, FlyoutState> FlyoutStates = new();

    private class FlyoutState
    {
        public int HoveredIndex { get; set; } = -1;
        public Dictionary<int, FlyoutState> Submenus { get; set; } = new();
    }

    /// <summary>
    /// Creates a flyout menu at the specified position
    /// </summary>
    public static void Flyout(this Gui gui, ref bool isOpen, Vector2 position, Action<FlyoutBuilder> buildMenu,
        float minWidth = 150,
        float itemHeight = 32,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? textColor = null,
        Color? hoverColor = null,
        Color? separatorColor = null,
        Color? disabledColor = null,
        float fontSize = 12,
        float padding = 8,
        float borderRadius = 4,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        if (!isOpen) return;

        var id = gui.NodeId(filePath, lineNumber);
        var state = GetOrCreateFlyoutState(id);

        var builder = new FlyoutBuilder();
        buildMenu(builder);

        if (builder.Items.Count == 0) return;

        var menuWidth = CalculateFlyoutWidth(builder.Items, fontSize, padding, minWidth);
        var menuHeight = builder.Items.Count * itemHeight;

        // Adjust position to keep menu on screen
        var adjustedPosition = ConstrainToScreen(gui, position, menuWidth, menuHeight);

        using (gui.Node(menuWidth, menuHeight, filePath: filePath, lineNumber: lineNumber)
                   .Left(adjustedPosition.X)
                   .Top(adjustedPosition.Y)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var bgColor = backgroundColor ?? Color.White;
                var borderColorFinal = borderColor ?? Color.FromArgb(255, 200, 200, 200);

                gui.DrawBackgroundRect(bgColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);

                HandleFlyoutInteraction(gui, state, builder.Items, gui.CurrentNode.Rect, itemHeight, ref isOpen);
            }

            // Render menu items
            for (var i = 0; i < builder.Items.Count; i++)
                RenderFlyoutItem(gui, state, builder.Items[i], i, menuWidth, itemHeight,
                    textColor, hoverColor, separatorColor, disabledColor, fontSize, padding);
        }

        // Handle click outside to close
        if (gui.Pass == Pass.Pass2Render && isOpen && gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = gui.Input.MousePosition;
            var menuRect = new Rect(adjustedPosition.X, adjustedPosition.Y, menuWidth, menuHeight);
            if (!IsMouseInRect(mousePos, menuRect))
            {
                isOpen = false;
                CloseFlyoutRecursive(state);
            }
        }
    }

    /// <summary>
    /// Creates a menu bar with flyout menus
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
        using (gui.Node().Height(height).Direction(Axis.Horizontal).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var bgColor = backgroundColor ?? Color.FromArgb(248, 249, 250);
                gui.DrawBackgroundRect(bgColor);
                gui.DrawRectBorder(gui.CurrentNode.Rect, Color.FromArgb(200, 200, 200), 1f);
            }

            var builder = new MenuBarBuilder(gui, height, textColor, hoverColor, fontSize, padding);
            buildMenus(builder);
        }
    }

    private static FlyoutState GetOrCreateFlyoutState(string id)
    {
        return FlyoutStates.TryGetValue(id, out var state) ? state : FlyoutStates[id] = new FlyoutState();
    }

    private static void HandleFlyoutInteraction(Gui gui, FlyoutState state, List<FlyoutItem> items, Rect rect,
        float itemHeight, ref bool isOpen)
    {
        var mousePos = gui.Input.MousePosition;
        var previousHovered = state.HoveredIndex;
        state.HoveredIndex = -1;

        if (IsMouseInRect(mousePos, rect))
        {
            var relativeY = mousePos.Y - rect.Y;
            var itemIndex = Math.Max(0, Math.Min((int)(relativeY / itemHeight), items.Count - 1));

            if (!items[itemIndex].IsSeparator) state.HoveredIndex = itemIndex;

            if (gui.Input.IsMouseButtonPressed(MouseButton.Left) && state.HoveredIndex >= 0)
            {
                var item = items[state.HoveredIndex];
                if (item.Enabled)
                {
                    if (item.HasSubmenu)
                    {
                        // Handle submenu (simplified for now)
                        var submenuPos = new Vector2(rect.X + rect.W, rect.Y + state.HoveredIndex * itemHeight);
                        // Submenu handling would go here
                    }
                    else
                    {
                        item.Action?.Invoke();
                        isOpen = false;
                        CloseFlyoutRecursive(state);
                    }
                }
            }
        }
    }

    private static void RenderFlyoutItem(Gui gui, FlyoutState state, FlyoutItem item, int index,
        float width, float height, Color? textColor, Color? hoverColor, Color? separatorColor,
        Color? disabledColor, float fontSize, float padding)
    {
        using (gui.Node(width, height).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                if (item.IsSeparator)
                {
                    var rect = gui.CurrentNode.Rect;
                    var sepColor = separatorColor ?? Color.FromArgb(255, 220, 220, 220);
                    var sepY = rect.Y + rect.H * 0.5f;
                    gui.DrawLine(new Vector2(rect.X + padding, sepY),
                        new Vector2(rect.X + rect.W - padding, sepY), sepColor);
                    return;
                }

                var isHovered = index == state.HoveredIndex;
                var itemColor = item.Enabled ? textColor ?? Color.Black : disabledColor ?? Color.Gray;

                if (isHovered && item.Enabled)
                {
                    var hoverColorFinal = hoverColor ?? Color.FromArgb(255, 230, 230, 230);
                    gui.DrawBackgroundRect(hoverColorFinal);
                }

                using (gui.Node().Padding(padding).Direction(Axis.Horizontal).Enter())
                {
                    gui.DrawText(item.Text, fontSize, itemColor, centerInRect: false);

                    if (item.HasSubmenu)
                    {
                        gui.Node().Expand();

                        gui.DrawText("▶", fontSize * 0.8f, itemColor, centerInRect: false);
                    }
                    else if (!string.IsNullOrEmpty(item.Shortcut))
                    {
                        gui.Node().Expand();

                        gui.DrawText(item.Shortcut, fontSize * 0.9f, Color.Gray, centerInRect: false);
                    }
                }
            }
        }
    }

    private static void CloseFlyoutRecursive(FlyoutState state)
    {
        state.HoveredIndex = -1;

        foreach (var submenu in state.Submenus.Values) CloseFlyoutRecursive(submenu);

        state.Submenus.Clear();
    }

    private static float CalculateFlyoutWidth(List<FlyoutItem> items, float fontSize, float padding, float minWidth)
    {
        var font = new SKFont { Size = fontSize };
        var maxWidth = minWidth;

        foreach (var item in items.Where(i => !i.IsSeparator))
        {
            font.MeasureText(item.Text, out var textBounds);
            var itemWidth = textBounds.Width + padding * 2;

            if (!string.IsNullOrEmpty(item.Shortcut))
            {
                font.MeasureText(item.Shortcut, out var shortcutBounds);
                itemWidth += shortcutBounds.Width + padding;
            }

            if (item.HasSubmenu) itemWidth += 20; // Space for arrow

            maxWidth = Math.Max(maxWidth, itemWidth);
        }

        return maxWidth;
    }
}

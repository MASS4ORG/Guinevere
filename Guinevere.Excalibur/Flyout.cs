using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    class FlyoutState
    {
        public int HoveredIndex { get; set; } = -1;
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
        float fontSize = ControlMetrics.CompactFontSize,
        float padding = ControlMetrics.Spacing,
        float borderRadius = ControlMetrics.CornerRadius,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.CompactFontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);
        borderRadius = gui.ControlStyle.CornerRadiusOr(borderRadius);

        if (!isOpen) return;

        var id = gui.NodeId(filePath, lineNumber);
        var state = GetOrCreateFlyoutState(gui, id);

        var builder = new FlyoutBuilder();
        buildMenu(builder);

        if (builder.Items.Count == 0) return;

        var menuWidth = CalculateFlyoutWidth(builder.Items, fontSize, padding, minWidth);
        var menuHeight = builder.Items.Count * itemHeight;

        // Adjust position to keep menu on screen
        var adjustedPosition = ConstrainToScreen(gui, position, menuWidth, menuHeight);

        // ReSharper disable once ExplicitCallerInfoArgument - keep the caller's original location for a stable NodeId
        using (gui.Node(menuWidth, menuHeight, filePath: filePath, lineNumber: lineNumber)
                   .AbsoluteScreen(adjustedPosition.X, adjustedPosition.Y)
                   .Enter())
        {
            using var focusScope = gui.EnterFocusNavigationScope($"{gui.CurrentNode.Id}/focus");
            focusScope.SetActive();
            if (gui.Pass == Pass.Pass2Render)
            {
                var bgColor = backgroundColor ?? gui.ControlStyle.Popup;
                var borderColorFinal = borderColor ?? gui.ControlStyle.Border;

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

    static FlyoutState GetOrCreateFlyoutState(Gui gui, string id) =>
        gui.ControlState(id, () => new FlyoutState());

    static void HandleFlyoutInteraction(Gui gui, FlyoutState state, List<FlyoutItem> items, Rect rect,
        float itemHeight, ref bool isOpen)
    {
        var mousePos = gui.Input.MousePosition;
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

    static void RenderFlyoutItem(Gui gui, FlyoutState state, FlyoutItem item, int index,
        float width, float height, Color? textColor, Color? hoverColor, Color? separatorColor,
        Color? disabledColor, float fontSize, float padding)
    {
        using (gui.Node(width, height).Enter())
        {
            if (item.IsSeparator)
            {
                if (gui.Pass != Pass.Pass2Render) return;

                var rect = gui.CurrentNode.Rect;
                var sepColor = separatorColor ?? gui.ControlStyle.Border;
                var sepY = rect.Y + rect.H * 0.5f;
                gui.DrawLine(new Vector2(rect.X + padding, sepY),
                    new Vector2(rect.X + rect.W - padding, sepY), sepColor);
                return;
            }

            var isHovered = index == state.HoveredIndex;
            var itemColor = item.Enabled ? textColor ?? gui.ControlStyle.Text : disabledColor ?? gui.ControlStyle.TextDim;

            if (gui.Pass == Pass.Pass2Render) gui.RegisterFocusable(canReceiveFocus: item.Enabled);

            if (isHovered && item.Enabled)
            {
                var hoverColorFinal = hoverColor ?? gui.ControlStyle.SurfaceHover;
                gui.DrawBackgroundRect(hoverColorFinal);
            }

            // Built in both passes: a node created only during the render pass never took part in
            // layout, so every label drew at the menu's origin instead of on its own row.
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

                    gui.DrawText(item.Shortcut, fontSize * 0.9f, gui.ControlStyle.TextDim, centerInRect: false);
                }
            }
        }
    }

    static void CloseFlyoutRecursive(FlyoutState state)
    {
        state.HoveredIndex = -1;
    }

    static float CalculateFlyoutWidth(List<FlyoutItem> items, float fontSize, float padding, float minWidth,
        bool hasCheckColumn = false)
    {
        var font = new SKFont { Size = fontSize };
        var maxWidth = minWidth;
        var checkColumnWidth = hasCheckColumn ? 18f : 0f;

        foreach (var item in items.Where(i => !i.IsSeparator))
        {
            font.MeasureText(item.Text, out var textBounds);
            var itemWidth = textBounds.Width + padding * 2 + checkColumnWidth;

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

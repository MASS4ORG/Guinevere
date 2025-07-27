using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    private static readonly Dictionary<string, PopupState> PopupStates = new();

    private class PopupState
    {
        public bool IsOpen { get; set; }
        public Vector2 Position { get; set; }
        public bool CloseOnClickOutside { get; set; } = true;
        public bool CloseOnEscape { get; set; } = true;
    }

    /// <summary>
    /// Creates a popup that can be opened/closed with internal state management
    /// </summary>
    public static void Popup(this Gui gui, ref bool isOpen, Action content,
        float width = 300,
        float height = 200,
        string title = "",
        Vector2? position = null,
        bool modal = false,
        bool closeOnClickOutside = true,
        bool closeOnEscape = true,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? titleBarColor = null,
        Color? titleTextColor = null,
        float titleBarHeight = 30,
        float borderRadius = 6,
        float borderWidth = 1,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var id = gui.NodeId(filePath, lineNumber);
        var state = GetOrCreatePopupState(id, position, closeOnClickOutside, closeOnEscape);

        // Sync external state with internal state
        if (isOpen != state.IsOpen)
        {
            state.IsOpen = isOpen;
            if (isOpen && position.HasValue) state.Position = position.Value;
        }

        // Always create popup structure for consistency
        HandlePopupInteraction(gui, state);
        RenderPopup(gui, state, content, width, height, title, backgroundColor, borderColor,
            titleBarColor, titleTextColor, titleBarHeight, borderRadius, borderWidth);

        isOpen = state.IsOpen;
    }

    /// <summary>
    /// Creates a popup that returns the open state without modifying the input
    /// </summary>
    public static bool Popup(this Gui gui, bool isOpen, Action content,
        float width = 300,
        float height = 200,
        string title = "",
        Vector2? position = null,
        bool modal = false,
        bool closeOnClickOutside = true,
        bool closeOnEscape = true,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? titleBarColor = null,
        Color? titleTextColor = null,
        float titleBarHeight = 30,
        float borderRadius = 6,
        float borderWidth = 1,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var temp = isOpen;
        gui.Popup(ref temp, content, width, height, title, position, modal, closeOnClickOutside,
            closeOnEscape, backgroundColor, borderColor, titleBarColor, titleTextColor,
            titleBarHeight, borderRadius, borderWidth, filePath, lineNumber);
        return temp;
    }

    /// <summary>
    /// Creates a modal popup (blocks interaction with background)
    /// </summary>
    public static void ModalPopup(this Gui gui, ref bool isOpen, Action content,
        float width = 300,
        float height = 200,
        string title = "",
        Vector2? position = null,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? titleBarColor = null,
        Color? titleTextColor = null,
        Color? overlayColor = null,
        float titleBarHeight = 30,
        float borderRadius = 6,
        float borderWidth = 1,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // Always create overlay node for consistent structure
        using (gui.Node(gui.ScreenRect.W, gui.ScreenRect.H).Left(0).Top(0).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
                // Only render overlay when modal is open
                if (isOpen)
                {
                    var overlay = overlayColor ?? Color.FromArgb(128, 0, 0, 0);
                    gui.DrawRect(gui.CurrentNode.Rect, overlay);
                }
        }

        gui.Popup(ref isOpen, content, width, height, title,
            position ?? new Vector2(gui.ScreenRect.W * 0.5f - width * 0.5f, gui.ScreenRect.H * 0.5f - height * 0.5f),
            true, true, true,
            backgroundColor, borderColor, titleBarColor, titleTextColor,
            titleBarHeight, borderRadius, borderWidth, filePath, lineNumber);
    }

    /// <summary>
    /// Creates a tooltip popup that follows the mouse
    /// </summary>
    public static void Tooltip(this Gui gui, string text, bool show = true,
        Vector2? offset = null,
        float maxWidth = 200,
        Color? backgroundColor = null,
        Color? textColor = null,
        Color? borderColor = null,
        float fontSize = 12,
        float padding = 8,
        float borderRadius = 4,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // Always create tooltip node for consistency
        var mousePos = gui.Input.MousePosition;
        var tooltipOffset = offset ?? new Vector2(10, -25);
        var tooltipPos = mousePos + tooltipOffset;

        // Calculate tooltip size
        var font = new SKFont { Size = fontSize };
        var tooltipText = text;
        font.MeasureText(tooltipText, out var textBounds);
        var tooltipWidth = Math.Min(textBounds.Width + padding * 2, maxWidth);
        var tooltipHeight = textBounds.Height + padding * 2;

        // Adjust position to keep tooltip on screen
        tooltipPos = ConstrainToScreen(gui, tooltipPos, tooltipWidth, tooltipHeight);

        using (gui.Node(tooltipWidth, tooltipHeight, filePath: filePath, lineNumber: lineNumber)
                   .Left(tooltipPos.X)
                   .Top(tooltipPos.Y)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
                // Only render background when shown and text is not empty
                if (show && !string.IsNullOrEmpty(text))
                {
                    var bgColor = backgroundColor ?? Color.FromArgb(240, 255, 255, 255);
                    var borderColorFinal = borderColor ?? Color.FromArgb(255, 180, 180, 180);

                    gui.DrawBackgroundRect(bgColor, borderRadius);
                    gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
                }

            // Always draw text for consistency, but make transparent when hidden
            var textColorFinal = show && !string.IsNullOrEmpty(text) ? textColor ?? Color.Black : Color.Transparent;
            gui.DrawText(tooltipText, fontSize, textColorFinal, centerInRect: false);
        }
    }

    /// <summary>
    /// Creates a context menu popup
    /// </summary>
    public static void ContextMenu(this Gui gui, ref bool isOpen, Action<ContextMenuBuilder> buildMenu,
        Vector2? position = null,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? hoverColor = null,
        float itemHeight = 24,
        float minWidth = 120,
        float borderRadius = 4,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // Always create menu node for consistency
        var menuPos = position ?? gui.Input.MousePosition;
        var builder = new ContextMenuBuilder();
        buildMenu(builder);

        var menuWidth = Math.Max(minWidth, builder.CalculateWidth());
        var menuHeight = builder.Items.Count * itemHeight;

        menuPos = ConstrainToScreen(gui, menuPos, menuWidth, menuHeight);

        using (gui.Node(menuWidth, menuHeight, filePath: filePath, lineNumber: lineNumber)
                   .Left(menuPos.X)
                   .Top(menuPos.Y)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
                // Only render background when open
                if (isOpen)
                {
                    var bgColor = backgroundColor ?? Color.White;
                    var borderColorFinal = borderColor ?? Color.FromArgb(255, 180, 180, 180);

                    gui.DrawBackgroundRect(bgColor, borderRadius);
                    gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, 1f, borderRadius);
                }

            RenderContextMenuItems(gui, builder.Items, ref isOpen, itemHeight, hoverColor, isOpen);
        }

        // Handle click outside to close - check after rendering the menu
        if (gui.Pass == Pass.Pass2Render && isOpen && gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = gui.Input.MousePosition;
            var menuRect = new Rect(menuPos.X, menuPos.Y, menuWidth, menuHeight);
            if (!IsMouseInRect(mousePos, menuRect)) isOpen = false;
        }
    }

    // Core implementation helpers
    private static PopupState GetOrCreatePopupState(string id, Vector2? position,
        bool closeOnClickOutside, bool closeOnEscape)
    {
        return PopupStates.TryGetValue(id, out var state)
            ? state
            : PopupStates[id] = new PopupState
            {
                Position = position ?? Vector2.Zero,
                CloseOnClickOutside = closeOnClickOutside,
                CloseOnEscape = closeOnEscape
            };
    }

    private static void HandlePopupInteraction(Gui gui, PopupState state)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        // Handle escape key
        if (state.CloseOnEscape && gui.Input.IsKeyPressed(KeyboardKey.Escape)) state.IsOpen = false;
    }

    private static void RenderPopup(Gui gui, PopupState state, Action content, float width, float height,
        string title, Color? backgroundColor, Color? borderColor, Color? titleBarColor,
        Color? titleTextColor, float titleBarHeight, float borderRadius, float borderWidth)
    {
        var totalHeight = string.IsNullOrEmpty(title) ? height : height + titleBarHeight;

        using (gui.Node(width, totalHeight)
                   .Left(state.Position.X)
                   .Top(state.Position.Y)
                   .Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                // Only render visually when popup is open
                if (state.IsOpen)
                {
                    var bgColor = backgroundColor ?? Color.White;
                    var borderColorFinal = borderColor ?? Color.FromArgb(255, 180, 180, 180);

                    gui.DrawBackgroundRect(bgColor, borderRadius);
                    gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, borderWidth, borderRadius);
                }

                // Handle escape key
                HandlePopupInteraction(gui, state);
            }

            // Always create title bar node for consistency
            if (!string.IsNullOrEmpty(title))
                RenderPopupTitleBar(gui, title, width, titleBarHeight, titleBarColor, titleTextColor, state.IsOpen);

            // Always create content area node for consistency
            var contentY = string.IsNullOrEmpty(title) ? 0 : titleBarHeight;
            using (gui.Node(width, height)
                       .Top(contentY)
                       .Padding(8)
                       .Enter())
            {
                // Only invoke content when popup is open
                if (state.IsOpen) content.Invoke();
            }
        }

        // Handle click outside to close - check after rendering the popup
        if (gui.Pass == Pass.Pass2Render && state is { IsOpen: true, CloseOnClickOutside: true } &&
            gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            var mousePos = gui.Input.MousePosition;
            var popupRect = new Rect(state.Position.X, state.Position.Y, width, totalHeight);
            if (!IsMouseInRect(mousePos, popupRect)) state.IsOpen = false;
        }
    }

    private static void RenderPopupTitleBar(Gui gui, string title, float width, float height,
        Color? titleBarColor, Color? titleTextColor, bool isOpen)
    {
        using (gui.Node(width, height).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && isOpen)
            {
                var titleBgColor = titleBarColor ?? Color.FromArgb(255, 240, 240, 240);
                gui.DrawBackgroundRect(titleBgColor);
            }

            // Always draw text for consistency, but make transparent when closed
            var titleColorFinal = isOpen ? titleTextColor ?? Color.Black : Color.Transparent;
            gui.DrawText(title, color: titleColorFinal, centerInRect: false);
        }
    }

    private static void RenderContextMenuItems(Gui gui, List<ContextMenuItem> items, ref bool isOpen,
        float itemHeight, Color? hoverColor, bool menuIsOpen)
    {
        foreach (var item in items)
        {
            using (gui.Node().Height(itemHeight).Direction(Axis.Horizontal).Padding(8).Enter())
            {
                if (gui.Pass == Pass.Pass2Render)
                    // Only handle interaction when menu is open
                    if (menuIsOpen)
                    {
                        var interactable = gui.GetInteractable();
                        var isHovered = interactable.OnHover();
                        var isClicked = interactable.OnClick();

                        if (isHovered)
                        {
                            var hoverColorFinal = hoverColor ?? Color.FromArgb(255, 240, 240, 240);
                            gui.DrawBackgroundRect(hoverColorFinal);
                        }

                        if (isClicked && item.Action != null)
                        {
                            item.Action();
                            isOpen = false;
                            return;
                        }
                    }

                // Always render text for consistency, but make transparent when closed
                var textColor = item.Enabled ? Color.Black : Color.Gray;
                if (!menuIsOpen) textColor = Color.Transparent;

                gui.DrawText(item.Text, color: textColor, centerInRect: false);
            }
        }
    }

    private static Vector2 ConstrainToScreen(Gui gui, Vector2 position, float width, float height)
    {
        var screen = gui.ScreenRect;
        var constrainedX = Math.Max(0, Math.Min(position.X, screen.W - width));
        var constrainedY = Math.Max(0, Math.Min(position.Y, screen.H - height));
        return new Vector2(constrainedX, constrainedY);
    }


    /// <summary>
    /// Clears all popup states (useful for cleanup)
    /// </summary>
    public static void ClearPopupStates(this Gui gui)
    {
        PopupStates.Clear();
    }
}

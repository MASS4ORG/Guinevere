using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    const int PopupZIndex = 9_000;
    const int TooltipZIndex = 11_000;

    class PopupState
    {
        public bool IsOpen { get; set; }
        public Vector2 Position { get; set; }
        public bool CloseOnClickOutside { get; set; } = true;
        public bool CloseOnEscape { get; set; } = true;

        /// <summary>
        /// Set on the frame the popup opens. The press that opens a popup is outside it by definition —
        /// it is on the button — so without this the click-outside rule closes it again immediately.
        /// </summary>
        public bool JustOpened { get; set; }
    }

    class TooltipState
    {
        public bool WasHovering { get; set; }
        public float EnteredAt { get; set; }

        /// <summary>The anchor's rect from the previous frame. Rects handed to the widget mid-pass1
        /// are not laid out yet, so hover tests and the tooltip position use last frame's rect.</summary>
        public Rect AnchorRect { get; set; }
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
        float titleBarHeight = 30,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        id ??= gui.NodeId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = GetOrCreatePopupState(gui, id, position, closeOnClickOutside, closeOnEscape);

        // Sync external state with internal state
        if (isOpen != state.IsOpen)
        {
            state.IsOpen = isOpen;
            state.JustOpened = isOpen;
            if (isOpen && position.HasValue) state.Position = position.Value;
        }
        else if (isOpen && position.HasValue)
        {
            state.Position = position.Value;
        }

        // Always create popup structure for consistency
        HandlePopupInteraction(gui, state);
        if (modal) RenderOverlay(gui, state.IsOpen, PopupZIndex - 1, id + "/overlay");
        RenderPopup(gui, state, content, width, height, title, titleBarHeight, classes, id);

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
        float titleBarHeight = 30,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var temp = isOpen;
        gui.Popup(ref temp, content, width, height, title, position, modal, closeOnClickOutside,
            closeOnEscape, titleBarHeight, classes, id, filePath, lineNumber);
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
        float titleBarHeight = 30,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        gui.Popup(ref isOpen, content, width, height, title,
            position ?? new Vector2(gui.ScreenRect.W * 0.5f - width * 0.5f, gui.ScreenRect.H * 0.5f - height * 0.5f),
            true, true, true,
            titleBarHeight, classes, id, filePath, lineNumber);
    }

    /// <summary>
    /// Creates a tooltip popup that follows the mouse
    /// </summary>
    public static void Tooltip(this Gui gui, string text, bool show = true,
        Vector2? offset = null,
        float maxWidth = 200,
        float fontSize = ControlMetrics.CompactFontSize,
        float padding = ControlMetrics.Spacing,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.CompactFontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        ExcaliburStyles.Ensure(gui);
        // Always create tooltip node for consistency
        var mousePos = gui.Input.MousePosition;
        var tooltipOffset = offset ?? new Vector2(10, -25);
        var tooltipPos = mousePos + tooltipOffset;

        // Calculate tooltip size
        using var font = new SKFont { Size = fontSize };
        var tooltipText = text;
        font.MeasureText(tooltipText, out var textBounds);
        var tooltipWidth = Math.Min(textBounds.Width + padding * 2, maxWidth);
        var tooltipHeight = textBounds.Height + padding * 2;

        // Adjust position to keep tooltip on screen
        tooltipPos = ConstrainToScreen(gui, tooltipPos, tooltipWidth, tooltipHeight);

        // ReSharper disable once ExplicitCallerInfoArgument - keep the caller's original location for a stable NodeId
        using (gui.StyledNode("tooltip", classes, id, modifiers: show && !string.IsNullOrEmpty(text) ? [] : ["closed"],
                       filePath: filePath, lineNumber: lineNumber).Width(tooltipWidth).Height(tooltipHeight)
                   .AbsoluteScreen(tooltipPos.X, tooltipPos.Y).HitTestVisible(false)
                   .ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            gui.SetZIndex(TooltipZIndex);
            gui.SetEscapesAncestorClips();

            gui.DrawText(tooltipText, fontSize, centerInRect: false).HitTestVisible(false);
        }
    }

    /// <summary>
    /// Creates a tooltip that appears after the pointer has hovered <paramref name="node"/> for
    /// <paramref name="delay"/> seconds, and hides as soon as the pointer leaves it. Positioned
    /// just below the node rather than glued to the cursor.
    /// </summary>
    public static void Tooltip(this Gui gui, LayoutNode node, string text, float delay = 0.45f,
        Vector2? offset = null,
        float maxWidth = 200,
        float fontSize = ControlMetrics.CompactFontSize,
        float padding = ControlMetrics.Spacing,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        fontSize = gui.ControlStyle.CompactFontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        id ??= gui.NodeId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = gui.ControlState(id, () => new TooltipState());
        var now = gui.Clock.Elapsed;

        var anchorRect = state.AnchorRect;
        var hovering = anchorRect.W > 0 && IsMouseInRect(gui.Input.MousePosition, anchorRect);

        if (hovering && !state.WasHovering) state.EnteredAt = now;
        state.WasHovering = hovering;

        var show = hovering && now - state.EnteredAt >= delay;

        if (gui.Pass == Pass.Pass2Render) state.AnchorRect = node.Rect;

        gui.Tooltip(text, show, offset ?? new Vector2(0, anchorRect.H),
            maxWidth, fontSize, padding, classes, id, filePath, lineNumber);
    }

    // Core implementation helpers
    static PopupState GetOrCreatePopupState(Gui gui, string id, Vector2? position,
        bool closeOnClickOutside, bool closeOnEscape) =>
        gui.ControlState(id, () => new PopupState
        {
            Position = position ?? Vector2.Zero,
            CloseOnClickOutside = closeOnClickOutside,
            CloseOnEscape = closeOnEscape
        });

    static void HandlePopupInteraction(Gui gui, PopupState state)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        // Handle escape key
        if (state.CloseOnEscape && gui.Input.IsKeyPressed(KeyboardKey.Escape)) state.IsOpen = false;
    }

    static void RenderPopup(Gui gui, PopupState state, Action content, float width, float height,
        string title, float titleBarHeight, IReadOnlyList<string>? classes, string id)
    {
        var totalHeight = string.IsNullOrEmpty(title) ? height : height + titleBarHeight;

        var popupNode = gui.StyledNode("popup", classes, id, modifiers: state.IsOpen ? [] : ["closed"]).Width(width).Height(totalHeight)
            .AbsoluteScreen(state.Position.X, state.Position.Y).HitTestVisible(state.IsOpen);
        if (state.IsOpen) popupNode.BlockInput();

        using (popupNode.Enter())
        {
            gui.SetZIndex(PopupZIndex);
            gui.SetEscapesAncestorClips();

            // Always create title bar node for consistency
            if (!string.IsNullOrEmpty(title))
                RenderPopupTitleBar(gui, title, width, titleBarHeight, state.IsOpen);

            // Always create content area node for consistency
            var contentY = string.IsNullOrEmpty(title) ? 0 : titleBarHeight;
            using (gui.StyledNode("popup-content").Width(width).Height(height).HitTestVisible(state.IsOpen)
                       .Top(contentY)
                       .Padding(8)
                       .Enter())
            {
                // Only invoke content when popup is open
                if (state.IsOpen)
                {
                    using var focusScope = gui.EnterFocusNavigationScope($"{gui.CurrentNode.Id}/focus");
                    focusScope.SetActive();
                    content.Invoke();
                }
            }
        }

        // Handle click outside to close - check after rendering the popup
        if (gui.Pass != Pass.Pass2Render || state is not { IsOpen: true, CloseOnClickOutside: true }) return;

        if (state.JustOpened)
        {
            state.JustOpened = false;
            return;
        }

        if (!gui.Input.IsMouseButtonPressed(MouseButton.Left)) return;

        var pointer = gui.Input.MousePosition;
        var popupRect = new Rect(state.Position.X, state.Position.Y, width, height);
        if (PointerOutsidePopup(popupNode, popupRect, pointer)) state.IsOpen = false;
    }

    static bool PointerOutsidePopup(LayoutNode popupNode, Rect popupRect, Vector2 pointer) =>
        !popupRect.Contains(pointer) && !PointerInChildOverlay(popupNode, pointer);

    static bool PointerInChildOverlay(LayoutNode node, Vector2 pointer)
    {
        foreach (var child in node.Children)
            if (child.Style.BlocksInput && child.Rect.Contains(pointer) || PointerInChildOverlay(child, pointer))
                return true;
        return false;
    }

    static void RenderPopupTitleBar(Gui gui, string title, float width, float height,
        bool isOpen)
    {
        using (gui.StyledNode("popup-title").Width(width).Height(height).Padding(8, 0).ContentAlignY(0.5f)
                   .HitTestVisible(isOpen).Enter())
        {
            gui.DrawText(title, centerInRect: false).HitTestVisible(isOpen);
        }
    }

    static void RenderOverlay(Gui gui, bool isOpen, int zIndex, string id)
    {
        using var overlay = gui.StyledNode("overlay", id: id, modifiers: isOpen ? [] : ["closed"])
            .Width(gui.ScreenRect.W).Height(gui.ScreenRect.H)
            .AbsoluteScreen(gui.ScreenRect.X, gui.ScreenRect.Y)
            .BlockInput(isOpen).HitTestVisible(isOpen).Enter();
        gui.SetZIndex(zIndex);
        gui.SetEscapesAncestorClips();
    }

    static Vector2 ConstrainToScreen(Gui gui, Vector2 position, float width, float height)
    {
        var screen = gui.ScreenRect;
        const float windowBorder = 2f;
        var left = screen.X + windowBorder;
        var top = screen.Y + windowBorder;
        var right = Math.Max(left, screen.X + screen.W - windowBorder - width);
        var bottom = Math.Max(top, screen.Y + screen.H - windowBorder - height);
        var constrainedX = Math.Clamp(position.X, left, right);
        var constrainedY = Math.Clamp(position.Y, top, bottom);
        return new Vector2(constrainedX, constrainedY);
    }


    /// <summary>
    /// Clears all popup states (useful for cleanup)
    /// </summary>
    public static void ClearPopupStates(this Gui gui)
    {
        gui.ClearControlStates<PopupState>();
    }
}

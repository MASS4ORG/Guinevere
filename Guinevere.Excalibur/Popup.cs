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

        /// <summary>The previous frame's anchor where it was drawn, for the hover test.</summary>
        public Rect AnchorScreenRect { get; set; }
    }

    /// <summary>
    /// Creates a popup that can be opened/closed with internal state management
    /// </summary>
    public static void Popup(this Gui gui, ref bool isOpen, Action content,
        float width = 300,
        float height = 200,
        Content title = default,
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
        id ??= gui.AutomaticId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = GetOrCreatePopupState(gui, id, position, closeOnClickOutside, closeOnEscape);

        // Sync external state with internal state
        if (isOpen != state.IsOpen)
        {
            state.IsOpen = isOpen;
            state.JustOpened = isOpen;
            if (isOpen && position.HasValue)
            {
                state.Position = position.Value;
            }
        }
        else if (isOpen && position.HasValue)
        {
            state.Position = position.Value;
        }

        // Always create popup structure for consistency
        HandlePopupInteraction(gui, state);
        if (modal)
        {
            RenderOverlay(gui, state.IsOpen, PopupZIndex - 1, id + "/overlay");
        }

        RenderPopup(gui, state, content, width, height, title, titleBarHeight, classes, id);

        isOpen = state.IsOpen;
    }

    /// <summary>
    /// Creates a popup that returns the open state without modifying the input
    /// </summary>
    public static bool Popup(this Gui gui, bool isOpen, Action content,
        float width = 300,
        float height = 200,
        Content title = default,
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
        Content title = default,
        Vector2? position = null,
        float titleBarHeight = 30,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        gui.Popup(ref isOpen, content, width, height, title,
            position ?? new Vector2(gui.LayoutBounds.W * 0.5f - width * 0.5f, gui.LayoutBounds.H * 0.5f - height * 0.5f),
            true, true, true,
            titleBarHeight, classes, id, filePath, lineNumber);
    }

    static readonly string[] ClosedModifier = ["closed"];

    /// <summary>
    /// A <c>tooltip</c> that follows the pointer, fitting its content plus the scope's control spacing, at most
    /// <paramref name="maxWidth"/> wide. The text takes the compact control font size unless the sheet sets one.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="content">What the tooltip shows: text, an icon, a localized string or other widgets.</param>
    /// <param name="show">When false the tooltip matches <c>:closed</c> (hidden by the default sheets).</param>
    /// <param name="offset">Offset from the pointer.</param>
    /// <param name="maxWidth">Widest the tooltip may grow.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id for <c>tooltip#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The tooltip's node; a tooltip reports no events.</returns>
    public static LayoutNode Tooltip(this Gui gui, Content content, bool show = true,
        Vector2? offset = null,
        float maxWidth = 200,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ExcaliburStyles.Ensure(gui);
        var fontSize = gui.ControlStyle.CompactFontSize;
        var padding = gui.ControlStyle.Spacing;

        // The box fits its content through layout; this text estimate only keeps it on screen in the build pass.
        var label = content.Label;
        var width = Math.Min((label is null ? 0f : gui.MeasureTextWidth(label, fontSize)) + padding * 2, maxWidth);
        var height = fontSize * gui.FontScale + padding * 2;
        var position = ConstrainToScreen(gui, gui.LocalMousePosition + (offset ?? new Vector2(10, -25)), width, height);

        var node = gui.StyledNode("tooltip", classes, id,
            modifiers: show && !content.IsEmpty ? NoModifiers : ClosedModifier,
            filePath: filePath, lineNumber: lineNumber);
        if (!node.Scope.HasLocal<LayoutNodeScopeTextSize>())
        {
            gui.SetTextSize(fontSize, node.Scope);
        }

        FitUnlessStyled(node);
        node.Padding(padding).MaxWidth(maxWidth).AbsoluteScreen(position.X, position.Y).HitTestVisible(false)
            .Direction(Axis.Horizontal).AlignContent(0.5f);
        using (node.Enter())
        {
            gui.SetZIndex(TooltipZIndex);
            gui.SetEscapesAncestorClips();
            gui.Content(content);
        }

        return node;
    }

    /// <summary>
    /// A tooltip that appears after the pointer has hovered <paramref name="node"/> for <paramref name="delay"/>
    /// seconds, and hides as soon as the pointer leaves it. Positioned just below the node rather than glued to the
    /// cursor.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="node">The node the tooltip describes, such as a button the caller just built.</param>
    /// <param name="content">What the tooltip shows.</param>
    /// <param name="delay">Seconds of hovering before it shows.</param>
    /// <param name="offset">Offset from the node's top-left; defaults to just below it.</param>
    /// <param name="maxWidth">Widest the tooltip may grow.</param>
    /// <param name="classes">Extra classes for the sheet.</param>
    /// <param name="id">Optional id for the hover state and <c>tooltip#id</c> rules; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The tooltip's node.</returns>
    public static LayoutNode Tooltip(this Gui gui, LayoutNode node, Content content, float delay = 0.45f,
        Vector2? offset = null,
        float maxWidth = 200,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        id ??= gui.AutomaticId(filePath, lineNumber);
        ExcaliburStyles.Ensure(gui);
        var state = gui.ControlState(id, static () => new TooltipState());
        var anchorRect = state.AnchorRect;
        var show = TooltipIsVisible(gui, state, state.AnchorScreenRect, delay);

        if (gui.Pass == Pass.Pass2Render)
        {
            state.AnchorRect = node.Rect;
            state.AnchorScreenRect = node.LocalToScreen(node.Rect);
        }

        return gui.Tooltip(content, show, offset ?? new Vector2(0, anchorRect.H), maxWidth, classes, id, filePath,
            lineNumber);
    }

    static bool TooltipIsVisible(Gui gui, TooltipState state, Rect anchorRect, float delay)
    {
        var now = gui.Clock.Elapsed;
        var hovering = anchorRect.W > 0 && IsMouseInRect(gui.Input.MousePosition, anchorRect);
        if (hovering && !state.WasHovering)
        {
            state.EnteredAt = now;
        }

        state.WasHovering = hovering;
        var waited = now - state.EnteredAt;
        if (hovering && waited < delay)
        {
            gui.RequestFrameIn(delay - waited);
        }

        return hovering && waited >= delay;
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
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        // Handle escape key
        if (state.CloseOnEscape && gui.IsCancelPressed)
        {
            state.IsOpen = false;
        }
    }

    static void RenderPopup(Gui gui, PopupState state, Action content, float width, float height,
        Content title, float titleBarHeight, IReadOnlyList<string>? classes, string id)
    {
        var totalHeight = title.IsEmpty ? height : height + titleBarHeight;

        var popupNode = gui.StyledNode("popup", classes, id, modifiers: state.IsOpen ? [] : ["closed"]).Width(width).Height(totalHeight)
            .AbsoluteScreen(state.Position.X, state.Position.Y).HitTestVisible(state.IsOpen);
        if (state.IsOpen)
        {
            popupNode.BlockInput();
        }

        using (popupNode.Enter())
        {
            gui.SetZIndex(PopupZIndex);
            gui.SetEscapesAncestorClips();

            // Always create title bar node for consistency
            if (!title.IsEmpty)
            {
                RenderPopupTitleBar(gui, title, width, titleBarHeight, state.IsOpen);
            }

            // Always create content area node for consistency
            var contentY = title.IsEmpty ? 0 : titleBarHeight;
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

        DismissPopupOutside(gui, state, popupNode);
    }

    static void DismissPopupOutside(Gui gui, PopupState state, LayoutNode popupNode)
    {
        if (gui.Pass != Pass.Pass2Render || state is not { IsOpen: true, CloseOnClickOutside: true })
        {
            return;
        }

        if (state.JustOpened)
        {
            state.JustOpened = false;
            return;
        }

        if (!gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        if (PointerOutsidePopup(popupNode, gui.Input.MousePosition))
        {
            state.IsOpen = false;
        }
    }

    static bool PointerOutsidePopup(LayoutNode popupNode, Vector2 pointer) =>
        !popupNode.ContainsScreenPoint(pointer) && !PointerInChildOverlay(popupNode, pointer);

    static bool PointerInChildOverlay(LayoutNode node, Vector2 pointer)
    {
        foreach (var child in node.Children)
        {
            if (child.Style.BlocksInput && child.ContainsScreenPoint(pointer) || PointerInChildOverlay(child, pointer))
            {
                return true;
            }
        }

        return false;
    }

    static void RenderPopupTitleBar(Gui gui, Content title, float width, float height,
        bool isOpen)
    {
        using (gui.StyledNode("popup-title").Width(width).Height(height).Padding(8, 0).AlignContentY(0.5f)
                   .HitTestVisible(isOpen).Enter())
        {
            gui.Content(title);
        }
    }

    static void RenderOverlay(Gui gui, bool isOpen, int zIndex, string id)
    {
        using var overlay = gui.StyledNode("overlay", id: id, modifiers: isOpen ? [] : ["closed"])
            .Width(gui.LayoutBounds.W).Height(gui.LayoutBounds.H)
            .AbsoluteScreen(gui.LayoutBounds.X, gui.LayoutBounds.Y)
            .BlockInput(isOpen).HitTestVisible(isOpen).Enter();
        gui.SetZIndex(zIndex);
        gui.SetEscapesAncestorClips();
    }

    static Vector2 ConstrainToScreen(Gui gui, Vector2 position, float width, float height)
    {
        var screen = gui.LayoutBounds;
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

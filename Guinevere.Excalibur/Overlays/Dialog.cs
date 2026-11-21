using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    const int DialogZIndex = 9_500;

    sealed class DialogState
    {
        public bool WasOpen { get; set; }
        public bool JustOpened { get; set; }
        public Vector2 DragOffset { get; set; }
        public bool IsDragging { get; set; }
        public Vector2 DragStartMouse { get; set; }
        public Vector2 DragStartOffset { get; set; }

        /// <summary>The dialog's size as last laid out, which centers and clamps it before the next layout.</summary>
        public Vector2 Size { get; set; }
    }

    /// <summary>
    /// A modal window over a dimming, input-blocking overlay: a title bar with an optional close
    /// button, a content area, and an optional footer for action buttons. Centered on screen, two fields wide and fitting
    /// its content unless a sheet or the returned node says otherwise.
    /// </summary>
    /// <remarks>
    /// Unlike <c>Popup</c>, a dialog blocks the whole screen — including whatever is behind the
    /// dimmed overlay — and, by default, closes only through its own close button, a footer action or
    /// Escape rather than a stray click outside it: a dialog's presence usually means the host expects
    /// a deliberate answer.
    /// </remarks>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="isOpen">Whether the dialog is shown; written back when it closes itself.</param>
    /// <param name="title">What the title bar shows: text, an icon, a localized string or other widgets.</param>
    /// <param name="content">Builds the body. Invoked only while the dialog is open.</param>
    /// <param name="footer">Builds an optional footer row below the body, for action buttons.</param>
    /// <param name="showCloseButton">Whether the title bar carries a × button.</param>
    /// <param name="draggable">Whether pressing the title bar and moving the pointer repositions the
    /// dialog. Each open starts centered again; the dragged position is not remembered afterward.</param>
    /// <param name="closeOnEscape">Whether Escape closes the dialog.</param>
    /// <param name="closeOnClickOutside">Whether a press on the dimmed overlay closes the dialog.</param>
    /// <param name="classes">Stylesheet classes for the dialog.</param>
    /// <param name="id">Optional id for <c>dialog#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The dialog's node, which is laid out even while closed.</returns>
    public static LayoutNode Dialog(this Gui gui, ref bool isOpen, Content title, Action content,
        Action? footer = null,
        bool showCloseButton = true,
        bool draggable = true,
        bool closeOnEscape = true,
        bool closeOnClickOutside = false,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0) =>
        DialogCore(gui, ref isOpen, title, content, footer, showCloseButton, draggable, closeOnEscape,
            closeOnClickOutside, classes, id ?? gui.AutomaticId(filePath, lineNumber),
            gui.ControlStyle.FieldWidth * 2f, Size.FitContent(), new Vector2(gui.ControlStyle.FieldWidth * 2f, 0f));

    /// <summary>
    /// The dialog behind <see cref="Dialog"/>; a host sets its default <paramref name="width"/> and
    /// <paramref name="height"/> (a sheet or the node still wins) and the <paramref name="fallback"/> size used to
    /// center it before it has been laid out once.
    /// </summary>
    static LayoutNode DialogCore(Gui gui, ref bool isOpen, Content title, Action content, Action? footer,
        bool showCloseButton, bool draggable, bool closeOnEscape, bool closeOnClickOutside,
        IReadOnlyList<string>? classes, string id, Size width, Size height, Vector2 fallback)
    {
        ExcaliburStyles.Ensure(gui);
        var state = gui.ControlState(id, static () => new DialogState());
        var titleBarHeight = DialogTitleHeight(gui);

        UpdateDialogState(gui, state, ref isOpen, closeOnEscape);

        // The overlay: dims the screen and, being a single node the size of it, blocks every click
        // meant for whatever is behind the dialog — not just the box itself.
        RenderOverlay(gui, isOpen, DialogZIndex, id + "/overlay");

        var size = state.Size == default ? fallback : state.Size;
        var position = DialogPosition(gui, state, size);
        HandleDialogDrag(gui, state, position, size.X, titleBarHeight, isOpen, draggable, showCloseButton);

        var dialogNode = CreateDialogNode(gui, isOpen, position, width, height, classes, id);
        using (dialogNode.Enter())
        {
            gui.SetZIndex(DialogZIndex + 1);
            gui.SetEscapesAncestorClips();

            if (TitleBar(gui, title, titleBarHeight, showCloseButton, isOpen))
            {
                isOpen = false;
            }

            DialogContent(gui, isOpen, content, id);
            DialogFooter(gui, isOpen, footer, DialogFooterHeight(gui), id);
        }

        if (gui.Pass == Pass.Pass2Render && dialogNode.Rect.W > 0)
        {
            RecordDialogSize(gui, state, dialogNode);
        }

        DismissDialogOutside(gui, state, ref isOpen, closeOnClickOutside, dialogNode);
        return dialogNode;
    }

    /// <summary>Top-left corner that centers the dialog at its size, plus the drag offset, kept on screen.</summary>
    static Vector2 DialogPosition(Gui gui, DialogState state, Vector2 size)
    {
        var centered = new Vector2(
            gui.LayoutBounds.X + (gui.LayoutBounds.W - size.X) * 0.5f,
            gui.LayoutBounds.Y + (gui.LayoutBounds.H - size.Y) * 0.5f);
        return ConstrainToScreen(gui, centered + state.DragOffset, size.X, size.Y);
    }

    /// <summary>Runs the title-bar drag on the render pass while the dialog is open and draggable.</summary>
    static void HandleDialogDrag(Gui gui, DialogState state, Vector2 position, float width, float titleBarHeight,
        bool isOpen, bool draggable, bool showCloseButton)
    {
        if (gui.Pass == Pass.Pass2Render && isOpen && draggable)
        {
            HandleTitleBarDrag(gui, state, position, width, titleBarHeight, showCloseButton);
        }
    }

    /// <summary>Creates the root node at its position, blocking input only while the dialog is open.</summary>
    static LayoutNode CreateDialogNode(Gui gui, bool isOpen, Vector2 position, Size width, Size height,
        IReadOnlyList<string>? classes, string id)
    {
        var dialogNode = gui.StyledNode("dialog", classes, id, modifiers: isOpen ? [] : ["closed"]);
        SizeUnlessStyled(dialogNode, width, height);
        dialogNode.Direction(Axis.Vertical).AbsoluteScreen(position.X, position.Y).HitTestVisible(isOpen);
        if (isOpen)
        {
            dialogNode.BlockInput();
        }

        return dialogNode;
    }

    /// <summary>Stores the laid-out size for next frame's centering and asks for a frame when it changed.</summary>
    static void RecordDialogSize(Gui gui, DialogState state, LayoutNode dialogNode)
    {
        var laidOut = new Vector2(dialogNode.Rect.W, dialogNode.Rect.H);
        if (Vector2.Distance(laidOut, state.Size) > 0.5f)
        {
            state.Size = laidOut;
            gui.RequestFrame();
        }
    }

    /// <summary>A dialog's title bar height, a little over a field.</summary>
    static float DialogTitleHeight(Gui gui) => gui.ControlStyle.FieldHeight + 4f;

    /// <summary>A dialog's footer row height, a field and a half.</summary>
    static float DialogFooterHeight(Gui gui) => gui.ControlStyle.FieldHeight * 1.5f;

    static void UpdateDialogState(Gui gui, DialogState state, ref bool isOpen, bool closeOnEscape)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        state.JustOpened = isOpen && !state.WasOpen;
        state.WasOpen = isOpen;
        if (state.JustOpened)
        {
            state.DragOffset = Vector2.Zero;
        }

        if (!isOpen)
        {
            state.IsDragging = false;
        }

        if (closeOnEscape && isOpen && gui.IsCancelPressed)
        {
            isOpen = false;
        }
    }

    static void DialogContent(Gui gui, bool isOpen, Action content, string id)
    {
        using (gui.StyledNode("dialog-content").ExpandWidth().ExpandHeight().Padding(16).HitTestVisible(isOpen)
                   .Enter())
        {
            if (isOpen)
            {
                using var focusScope = gui.EnterFocusNavigationScope($"{id}/focus");
                focusScope.SetActive();
                content.Invoke();
            }
        }
    }

    static void DialogFooter(Gui gui, bool isOpen, Action? footer, float height, string id)
    {
        if (footer is null)
        {
            return;
        }

        using (gui.StyledNode("dialog-footer").ExpandWidth().Height(height)
                   .Direction(Axis.Horizontal).Padding(16, 8).Gap(8).AlignContentX(1f)
                   .AlignContentY(0.5f).Enter())
        {
            if (isOpen)
            {
                using var focusScope = gui.EnterFocusNavigationScope($"{id}/focus");
                focusScope.SetActive();
                footer.Invoke();
            }
        }
    }

    static void DismissDialogOutside(Gui gui, DialogState state, ref bool isOpen, bool closeOnClickOutside,
        LayoutNode dialogNode)
    {
        if (gui.Pass != Pass.Pass2Render || !isOpen || !closeOnClickOutside || state.JustOpened)
        {
            return;
        }

        if (!gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        if (!dialogNode.ContainsScreenPoint(gui.Input.MousePosition))
        {
            isOpen = false;
        }
    }

    /// <summary>
    /// Presses on the title bar move the dialog; the close button's corner is excluded so a click
    /// there never starts a drag instead. The offset is measured from the press position every frame
    /// rather than accumulated from frame deltas, so it tracks the pointer exactly regardless of
    /// frame rate, the same way the scrollbar thumb's own drag does.
    /// </summary>
    static void HandleTitleBarDrag(Gui gui, DialogState state, Vector2 position, float width,
        float titleBarHeight, bool showCloseButton)
    {
        var mouse = gui.LocalMousePosition;

        if (state.IsDragging)
        {
            if (gui.Input.IsMouseButtonDown(MouseButton.Left))
            {
                state.DragOffset = state.DragStartOffset + (mouse - state.DragStartMouse);
            }
            else
            {
                state.IsDragging = false;
            }

            return;
        }

        // The click that just opened the dialog is not also the click that starts dragging it.
        if (state.JustOpened || !gui.Input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        var titleBarRect = new Rect(position.X, position.Y, width, titleBarHeight);
        if (!IsMouseInRect(mouse, titleBarRect))
        {
            return;
        }

        if (showCloseButton)
        {
            var closeZone = new Rect(position.X + width - titleBarHeight, position.Y,
                titleBarHeight, titleBarHeight);
            if (IsMouseInRect(mouse, closeZone))
            {
                return;
            }
        }

        state.IsDragging = true;
        state.DragStartMouse = mouse;
        state.DragStartOffset = state.DragOffset;
    }

    /// <summary>Draws the title bar. Returns true on the frame its close button was clicked.</summary>
    static bool TitleBar(Gui gui, Content title, float height, bool showCloseButton, bool isOpen)
    {
        using (gui.StyledNode("dialog-title").ExpandWidth().Height(height).HitTestVisible(isOpen).Enter())
        {
            using (gui.Node().Expand().Direction(Axis.Horizontal).Padding(16, 0).AlignContentY(0.5f).Enter())
            {
                using (gui.Node().Expand().AlignContentY(0.5f).Enter())
                {
                    gui.Content(title);
                }

                return showCloseButton && CloseButton(gui, height * 0.6f, isOpen);
            }
        }
    }

    /// <summary>Draws the × close button. Returns true on the frame it was clicked.</summary>
    static bool CloseButton(Gui gui, float size, bool isOpen)
    {
        using (gui.StyledNode("dialog-close").Width(size).Height(size)
                   .AlignContentX(0.5f).AlignContentY(0.5f).HitTestVisible(isOpen).Enter())
        {
            gui.DrawText(WidgetIcons.Xmark);
            if (gui.Pass != Pass.Pass2Render || !isOpen)
            {
                return false;
            }

            var interactable = gui.GetInteractable();
            var hot = interactable.OnHover();

            return hot && interactable.OnClick();
        }
    }
}

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
    }

    /// <summary>
    /// A modal window over a dimming, input-blocking overlay: a title bar with an optional close
    /// button, a content area, and an optional footer for action buttons. Centered on screen and sized
    /// to <paramref name="width"/> and <paramref name="height"/>.
    /// </summary>
    /// <remarks>
    /// Unlike <c>Popup</c>, a dialog blocks the whole screen — including whatever is behind the
    /// dimmed overlay — and, by default, closes only through its own close button, a footer action or
    /// Escape rather than a stray click outside it: a dialog's presence usually means the host expects
    /// a deliberate answer.
    /// </remarks>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="isOpen">Whether the dialog is shown; written back when it closes itself.</param>
    /// <param name="title">Text drawn in the title bar.</param>
    /// <param name="content">Builds the body. Invoked only while the dialog is open.</param>
    /// <param name="width">Dialog width.</param>
    /// <param name="height">Body height, before the title bar and footer.</param>
    /// <param name="footer">Builds an optional footer row below the body, for action buttons.</param>
    /// <param name="showCloseButton">Whether the title bar carries a × button.</param>
    /// <param name="draggable">Whether pressing the title bar and moving the pointer repositions the
    /// dialog. Each open starts centered again; the dragged position is not remembered afterward.</param>
    /// <param name="closeOnEscape">Whether Escape closes the dialog.</param>
    /// <param name="closeOnClickOutside">Whether a press on the dimmed overlay closes the dialog.</param>
    /// <param name="backgroundColor">Body fill; defaults to <see cref="Gui.Controls"/>'s popup color.</param>
    /// <param name="borderColor">Border color; defaults to <see cref="Gui.Controls"/>'s border color.</param>
    /// <param name="titleBarColor">Title bar fill; defaults to <paramref name="backgroundColor"/>.</param>
    /// <param name="titleTextColor">Title text color; defaults to <see cref="Gui.Controls"/>'s text color.</param>
    /// <param name="overlayColor">Dimming color behind the dialog.</param>
    /// <param name="titleBarHeight">Title bar height.</param>
    /// <param name="footerHeight">Footer row height, used only when <paramref name="footer"/> is given.</param>
    /// <param name="borderRadius">Corner radius of the dialog window.</param>
    /// <param name="borderWidth">Border thickness of the dialog window.</param>
    /// <param name="filePath">Caller-supplied; identifies this call site for its persistent state.</param>
    /// <param name="lineNumber">Caller-supplied; identifies this call site for its persistent state.</param>
    public static void Dialog(this Gui gui, ref bool isOpen, string title, Action content,
        float width = 400,
        float height = 300,
        Action? footer = null,
        bool showCloseButton = true,
        bool draggable = true,
        bool closeOnEscape = true,
        bool closeOnClickOutside = false,
        Color? backgroundColor = null,
        Color? borderColor = null,
        Color? titleBarColor = null,
        Color? titleTextColor = null,
        Color? overlayColor = null,
        float titleBarHeight = 36,
        float footerHeight = 48,
        float borderRadius = 8,
        float borderWidth = 1,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new DialogState());

        if (gui.Pass == Pass.Pass2Render)
        {
            state.JustOpened = isOpen && !state.WasOpen;
            state.WasOpen = isOpen;
            if (state.JustOpened) state.DragOffset = Vector2.Zero;
            if (!isOpen) state.IsDragging = false;

            if (closeOnEscape && isOpen && gui.Input.IsKeyPressed(KeyboardKey.Escape)) isOpen = false;
        }

        // The overlay: dims the screen and, being a single node the size of it, blocks every click
        // meant for whatever is behind the dialog — not just the box itself.
        var overlayNode = gui.Node(gui.ScreenRect.W, gui.ScreenRect.H).AbsoluteScreen(0, 0);
        if (isOpen) overlayNode.BlockInput();

        using (overlayNode.Enter())
        {
            gui.SetZIndex(DialogZIndex);
            gui.SetEscapesAncestorClips();

            if (gui.Pass == Pass.Pass2Render && isOpen)
                gui.DrawRect(gui.CurrentNode.Rect, overlayColor ?? Color.FromArgb(140, 0, 0, 0));
        }

        var hasFooter = footer is not null;
        var totalHeight = titleBarHeight + height + (hasFooter ? footerHeight : 0);
        var centered = new Vector2(
            gui.ScreenRect.X + (gui.ScreenRect.W - width) * 0.5f,
            gui.ScreenRect.Y + (gui.ScreenRect.H - totalHeight) * 0.5f);
        var position = ConstrainToScreen(gui, centered + state.DragOffset, width, totalHeight);

        if (gui.Pass == Pass.Pass2Render && isOpen && draggable)
            HandleTitleBarDrag(gui, state, position, width, titleBarHeight, showCloseButton);

        var bodyColor = backgroundColor ?? gui.Controls.Popup;
        var borderColorFinal = borderColor ?? gui.Controls.Border;

        var dialogNode = gui.Node(width, totalHeight).AbsoluteScreen(position.X, position.Y);
        if (isOpen) dialogNode.BlockInput();

        using (dialogNode.Enter())
        {
            gui.SetZIndex(DialogZIndex + 1);
            gui.SetEscapesAncestorClips();

            if (gui.Pass == Pass.Pass2Render && isOpen)
            {
                gui.DrawBackgroundRect(bodyColor, borderRadius);
                gui.DrawRectBorder(gui.CurrentNode.Rect, borderColorFinal, borderWidth, borderRadius);
            }

            if (TitleBar(gui, title, width, titleBarHeight, titleBarColor ?? bodyColor, titleTextColor,
                    borderRadius, showCloseButton, isOpen))
                isOpen = false;

            using (gui.Node(width, height).Top(titleBarHeight).Padding(16).Enter())
                if (isOpen) content.Invoke();

            if (hasFooter)
                using (gui.Node(width, footerHeight).Top(titleBarHeight + height)
                           .Direction(Axis.Horizontal).Padding(16, 8).Gap(8).ContentAlignX(1f)
                           .ContentAlignY(0.5f).Enter())
                {
                    if (gui.Pass == Pass.Pass2Render && isOpen)
                        gui.DrawRect(new Rect(gui.CurrentNode.Rect.X, gui.CurrentNode.Rect.Y,
                            gui.CurrentNode.Rect.W, borderWidth), borderColorFinal);

                    if (isOpen) footer!.Invoke();
                }
        }

        if (gui.Pass != Pass.Pass2Render || !isOpen || !closeOnClickOutside || state.JustOpened) return;
        if (!gui.Input.IsMouseButtonPressed(MouseButton.Left)) return;

        var dialogRect = new Rect(position.X, position.Y, width, totalHeight);
        if (!IsMouseInRect(gui.Input.MousePosition, dialogRect)) isOpen = false;
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
        var mouse = gui.Input.MousePosition;

        if (state.IsDragging)
        {
            if (gui.Input.IsMouseButtonDown(MouseButton.Left))
                state.DragOffset = state.DragStartOffset + (mouse - state.DragStartMouse);
            else
                state.IsDragging = false;

            return;
        }

        // The click that just opened the dialog is not also the click that starts dragging it.
        if (state.JustOpened || !gui.Input.IsMouseButtonPressed(MouseButton.Left)) return;

        var titleBarRect = new Rect(position.X, position.Y, width, titleBarHeight);
        if (!IsMouseInRect(mouse, titleBarRect)) return;

        if (showCloseButton)
        {
            var closeZone = new Rect(position.X + width - titleBarHeight, position.Y,
                titleBarHeight, titleBarHeight);
            if (IsMouseInRect(mouse, closeZone)) return;
        }

        state.IsDragging = true;
        state.DragStartMouse = mouse;
        state.DragStartOffset = state.DragOffset;
    }

    /// <summary>Draws the title bar. Returns true on the frame its close button was clicked.</summary>
    static bool TitleBar(Gui gui, string title, float width, float height, Color barColor,
        Color? textColor, float borderRadius, bool showCloseButton, bool isOpen)
    {
        using (gui.Node(width, height).Enter())
        {
            if (gui.Pass == Pass.Pass2Render && isOpen)
                gui.DrawBackgroundRect(barColor, borderRadius, Corner.Top);

            using (gui.Node().Expand().Direction(Axis.Horizontal).Padding(16, 0).ContentAlignY(0.5f).Enter())
            {
                using (gui.Node().Expand().Enter())
                    gui.DrawText(title, color: isOpen ? textColor ?? gui.Controls.Text : Color.Transparent,
                        centerInRect: false);

                return showCloseButton && CloseButton(gui, height * 0.6f, isOpen);
            }
        }
    }

    /// <summary>Draws the × close button. Returns true on the frame it was clicked.</summary>
    static bool CloseButton(Gui gui, float size, bool isOpen)
    {
        using (gui.Node(size, size).ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            if (gui.Pass != Pass.Pass2Render || !isOpen) return false;

            var interactable = gui.GetInteractable();
            var hot = interactable.OnHover();

            if (hot) gui.DrawBackgroundRect(gui.Controls.SurfaceHover, size * 0.5f);
            gui.DrawText("×", color: hot ? gui.Controls.Text : gui.Controls.TextDim);

            return hot && interactable.OnClick();
        }
    }
}

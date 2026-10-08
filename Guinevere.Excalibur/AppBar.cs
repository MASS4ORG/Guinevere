using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    internal sealed class AppBarState
    {
        internal IWindowChromeCapability? Window;
        /// <summary>The last decoration request, cached to avoid repeating native updates each frame.</summary>
        internal bool? NativeTitlebar;
        internal bool Maximized;
        internal Vector2 DragOffset;
        internal bool PressedOverControl;
        internal IWindowResizeCapability? ResizeWindow;
        internal bool ResizeEnabled;
        internal Rect ResizeBounds;
        internal Vector2 ResizePointer;
    }

    /// <summary>
    /// Opens a horizontal application bar for ordinary widgets and layout nodes.
    /// Passive content moves the window; optional resize handles replace native borders on supported backends.
    /// </summary>
    /// <param name="gui">The GUI that owns the application bar.</param>
    /// <param name="height">The bar height in logical desktop units; must be at least 24.</param>
    /// <param name="windowControls">Displays window controls and enables window chrome integration.</param>
    /// <param name="nativeTitlebar">Keeps the native window title bar and decorations visible.</param>
    /// <param name="classes">Stylesheet classes for the application bar.</param>
    /// <param name="id">Stable control and stylesheet identity.</param>
    /// <param name="resizable">Enables border and corner resizing while native decorations are hidden.</param>
    /// <param name="minimumWindowSize">The minimum client size in logical desktop units; defaults to 160 by 100.</param>
    /// <param name="filePath">The caller file path used to identify the application bar.</param>
    /// <param name="lineNumber">The caller line number used to identify the application bar.</param>
    public static AppBarScope AppBar(this Gui gui, float height = 36, bool windowControls = true,
        bool nativeTitlebar = false, bool resizable = false,
        Vector2? minimumWindowSize = null,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 24);

        ExcaliburStyles.Ensure(gui);
        id ??= gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, static () => new AppBarState());
        gui.Platform.TryGet<IWindowChromeCapability>(out var window);
        var chrome = windowControls ? window : null;
        var native = nativeTitlebar || chrome?.CanMove == false;
        if (gui.Pass == Pass.Pass1Build) state.Maximized = chrome?.IsMaximized == true;

        var bar = gui.StyledNode("appbar", classes, id).ExpandWidth().Height(height).Direction(Axis.Horizontal).Enter();
        DrawAppBarChrome(gui, state, chrome, native);
        AppBarResizeHandles(gui, state, chrome, resizable && !native, minimumWindowSize, id);
        var content = gui.Node().ExpandWidth().Height(height).Direction(Axis.Horizontal)
            .ContentAlignY(0.5f).Padding(8, 0).Gap(8).Enter();
        gui.SetClipped(true);
        return new AppBarScope(gui, bar, content, state, chrome, height);
    }

    static void DrawAppBarChrome(Gui gui, AppBarState state, IWindowChromeCapability? window, bool native)
    {
        if (gui.Pass != Pass.Pass2Render) return;
        UpdateAppBarChrome(state, window, native);
    }

    static void UpdateAppBarChrome(AppBarState state, IWindowChromeCapability? window, bool native)
    {
        if (state.Window is not null && !ReferenceEquals(state.Window, window))
            state.Window.DrawWindowTitlebar(true);
        if (window is not null && (!ReferenceEquals(state.Window, window) || state.NativeTitlebar != native))
            window.DrawWindowTitlebar(native);
        state.Window = window;
        state.NativeTitlebar = native;
    }

    internal static void EndAppBar(Gui gui, AppBarState state, IWindowChromeCapability? window,
        LayoutNodeScope content, LayoutNodeScope bar, float height)
    {
        if (gui.Pass == Pass.Pass2Render && window is not null) HandleAppBarDrag(gui, state, window);
        content.Dispose();
        if (window is not null) RenderWindowControls(gui, window, state.Maximized, height);
        bar.Dispose();
    }

    static void HandleAppBarDrag(Gui gui, AppBarState state, IWindowChromeCapability window)
    {
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left))
            state.PressedOverControl = AppBarPointerOverControl(gui, gui.CurrentNode);
        if (state.PressedOverControl) return;

        var interaction = gui.GetInteractable();
        if (interaction.OnClick(out var count) && count == 2)
        {
            ToggleWindowMaximize(window);
            return;
        }
        MoveAppBarWindow(gui, state, window, interaction);
    }

    static bool AppBarPointerOverControl(Gui gui, LayoutNode node)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.IsPointerInteractive && gui.GetInteractable(child).OnHover()) return true;
            if (AppBarPointerOverControl(gui, child)) return true;
        }
        return false;
    }

    static void MoveAppBarWindow(Gui gui, AppBarState state, IWindowChromeCapability window,
        InteractableElement interaction)
    {
        if (!window.CanMove || window.IsMaximized || !interaction.OnDrag(out _)) return;
        var pointer = window.PointerPosition;
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left)) state.DragOffset = pointer - window.Position;
        window.Position = pointer - state.DragOffset;
    }

    static void RenderWindowControls(Gui gui, IWindowChromeCapability window, bool maximized, float height)
    {
        if (WindowChromeButton(gui, 0, height)) window.Minimize();
        if (WindowChromeButton(gui, maximized ? 2 : 1, height)) ToggleWindowMaximize(window);
        if (WindowChromeButton(gui, 3, height)) window.RequestClose();
    }

    static void ToggleWindowMaximize(IWindowChromeCapability window)
    {
        if (window.IsMaximized) window.Restore();
        else window.Maximize();
    }

    static bool WindowChromeButton(Gui gui, int kind, float height)
    {
        using var scope = gui.StyledNode("window-button", modifiers: kind == 3 ? ["close"] : [])
            .Width(height).Height(height).Enter();
        if (gui.Pass != Pass.Pass2Render) return false;
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var interaction = gui.GetInteractable();
        if (interaction.OnClick()) gui.RequestFocus(FocusReason.Mouse);
        DrawWindowChromeGlyph(gui, kind, gui.CurrentNode.Rect);
        return interaction.OnClick() || (gui.HasFocus()
            && (gui.Input.IsKeyPressed(KeyboardKey.Enter) || gui.Input.IsKeyPressed(KeyboardKey.Space)));
    }

    static void DrawWindowChromeGlyph(Gui gui, int kind, Rect rect)
    {
        var x = rect.X + (rect.W - 10) * 0.5f;
        var y = rect.Y + (rect.H - 10) * 0.5f;
        var color = gui.CurrentNode.Scope.Get<LayoutNodeScopeTextColor>().Value;
        switch (kind)
        {
            case 0:
                gui.DrawLine(new Vector2(x, y + 5), new Vector2(x + 10, y + 5), color);
                break;
            case 1:
                gui.DrawRectBorder(new Rect(x, y, 10, 10), color, 1);
                break;
            case 2:
                gui.DrawRectBorder(new Rect(x + 2, y, 8, 8), color, 1);
                gui.DrawStyledBox(gui.ResolvePart("glyph-fill"), new Rect(x, y + 2, 8, 8));
                gui.DrawRectBorder(new Rect(x, y + 2, 8, 8), color, 1);
                break;
            default:
                gui.DrawLine(new Vector2(x, y), new Vector2(x + 10, y + 10), color);
                gui.DrawLine(new Vector2(x + 10, y), new Vector2(x, y + 10), color);
                break;
        }
    }
}

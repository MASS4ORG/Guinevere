using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    sealed class AppBarState
    {
        public IWindowChromeCapability? Window;
        public bool Maximized;
        public bool OverflowOpen;
        public float Width;
        public Rect OverflowAnchor;
        public Vector2 FrameOverflowPosition;
        public float LeadingWidth;
        public Vector2 DragOffset;
    }

    /// <summary>
    /// Draws a composable application bar and replaces native decorations when window chrome is available.
    /// Custom regions stay interactive; the title and unused space move the window and double-click to maximize.
    /// </summary>
    public static void AppBar(this Gui gui, Action<AppBarBuilder> build, float height = 36,
        bool windowControls = true, float minimumContentWidth = 160, Color? backgroundColor = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 24);
        ArgumentOutOfRangeException.ThrowIfNegative(minimumContentWidth);

        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new AppBarState());
        var builder = new AppBarBuilder();
        build(builder);
        gui.Platform.TryGet<IWindowChromeCapability>(out var window);
        var chrome = windowControls ? window : null;
        if (gui.Pass == Pass.Pass1Build)
        {
            state.Maximized = chrome?.IsMaximized == true;
            state.FrameOverflowPosition = new Vector2(state.OverflowAnchor.X,
                state.OverflowAnchor.Y + state.OverflowAnchor.H);
        }

        var visible = VisibleAppBarActions(gui, state, builder, chrome, height, minimumContentWidth);
        var overflow = visible < builder.Actions.Count;
        using (gui.Node().ExpandWidth().Height(height).Direction(Axis.Horizontal).Enter())
        {
            DrawAppBarBackground(gui, state, chrome, backgroundColor);
            RenderAppBarLeading(gui, state, builder, height);
            RenderAppBarContent(gui, state, builder, chrome, height);
            for (var i = 0; i < visible; i++)
                if (AppBarActionButton(gui, builder.Actions[i], height)) builder.Actions[i].Run();
            RenderAppBarOverflowButton(gui, state, overflow, height);
            if (chrome is not null) RenderWindowControls(gui, chrome, state.Maximized, height);
        }
        RenderAppBarOverflowMenu(gui, state, builder, visible, overflow, id);
    }

    static int VisibleAppBarActions(Gui gui, AppBarState state, AppBarBuilder builder,
        IWindowChromeCapability? chrome, float height, float minimumContentWidth)
    {
        var width = state.Width > 0 ? state.Width : gui.ScreenRect.W;
        var reserved = Math.Max(builder.LeadingWidth, state.LeadingWidth) + minimumContentWidth
            + (chrome is null ? 0 : height * 3)
            + (builder.MainContent is not null && chrome is not null ? 24 : 0);
        var capacity = Math.Max(0, (int)((width - reserved) / height));
        return builder.Actions.Count <= capacity ? builder.Actions.Count : Math.Max(0, capacity - 1);
    }

    static void DrawAppBarBackground(Gui gui, AppBarState state, IWindowChromeCapability? chrome, Color? color)
    {
        if (gui.Pass != Pass.Pass2Render) return;
        state.Width = gui.CurrentNode.Rect.W;
        gui.DrawBackgroundRect(color ?? gui.ControlStyle.Surface);
        if (chrome is null || ReferenceEquals(state.Window, chrome)) return;
        chrome.DrawWindowTitlebar(false);
        state.Window = chrome;
    }

    static void RenderAppBarLeading(Gui gui, AppBarState state, AppBarBuilder builder, float height)
    {
        if (builder.LeadingContent is null) return;
        using var scope = gui.Node().Width(builder.LeadingWidth > 0 ? builder.LeadingWidth : UnitValue.Fit)
            .Height(height).Enter();
        builder.LeadingContent(gui);
        if (gui.Pass == Pass.Pass2Render) state.LeadingWidth = gui.CurrentNode.Rect.W;
    }

    static void RenderAppBarContent(Gui gui, AppBarState state, AppBarBuilder builder,
        IWindowChromeCapability? chrome, float height)
    {
        using (gui.Node().ExpandWidth().Height(height).Enter())
        {
            gui.SetClipped(true);
            if (builder.MainContent is not null) builder.MainContent(gui);
            else
                using (gui.Node().ExpandWidth().Height(height).Padding(8, 0).ContentAlignY(0.5f).Enter())
                {
                    gui.DrawText(builder.TitleText, gui.ControlStyle.CompactFontSize, gui.ControlStyle.Text);
                    HandleAppBarDragRegion(gui, state, chrome);
                }
        }
        if (builder.MainContent is null || chrome is null) return;
        using (gui.Node(24, height).Enter()) HandleAppBarDragRegion(gui, state, chrome);
    }

    static void HandleAppBarDragRegion(Gui gui, AppBarState state, IWindowChromeCapability? chrome)
    {
        if (gui.Pass == Pass.Pass2Render && chrome is not null) HandleAppBarDrag(gui, state, chrome);
    }

    static bool AppBarActionButton(Gui gui, AppBarAction action, float height)
    {
        using var scope = gui.Node(height, height).Enter();
        gui.SetClipped(true);
        return gui.Button(action.Text, height, height, enabled: action.Enabled);
    }

    static void RenderAppBarOverflowButton(Gui gui, AppBarState state, bool overflow, float height)
    {
        if (!overflow) return;
        using var scope = gui.Node(height, height).Enter();
        if (gui.Button("…", height, height)) state.OverflowOpen = !state.OverflowOpen;
        if (gui.Pass == Pass.Pass2Render) state.OverflowAnchor = gui.CurrentNode.Rect;
    }

    static void RenderAppBarOverflowMenu(Gui gui, AppBarState state, AppBarBuilder builder,
        int visible, bool overflow, string id)
    {
        if (!overflow) state.OverflowOpen = false;
        gui.CascadeMenu(ref state.OverflowOpen, state.FrameOverflowPosition, menu =>
        {
            for (var i = visible; i < builder.Actions.Count; i++)
            {
                var action = builder.Actions[i];
                menu.Item(action.Text, action.Run, action.Shortcut, action.Enabled);
            }
        }, filePath: id, lineNumber: 0);
    }

    static void HandleAppBarDrag(Gui gui, AppBarState state, IWindowChromeCapability window)
    {
        var interactable = gui.GetInteractable();
        if (interactable.OnClick(out var count) && count == 2)
        {
            ToggleWindowMaximize(window);
            return;
        }
        if (!window.CanMove || window.IsMaximized || !interactable.OnDrag(out _)) return;
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left))
            state.DragOffset = window.PointerPosition - window.Position;
        window.Position = window.PointerPosition - state.DragOffset;
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
        using var scope = gui.Node(height, height).Enter();
        if (gui.Pass != Pass.Pass2Render) return false;
        gui.RegisterFocusable(canReceiveFocus: true, isInteractable: true);
        var interaction = gui.GetInteractable();
        if (interaction.OnClick()) gui.RequestFocus(FocusReason.Mouse);
        if (interaction.OnHover()) gui.DrawBackgroundRect(gui.ControlStyle.SurfaceHover);
        var rect = gui.CurrentNode.Rect;
        DrawWindowChromeGlyph(gui, kind, rect);
        return interaction.OnClick() || (gui.HasFocus()
            && (gui.Input.IsKeyPressed(KeyboardKey.Enter) || gui.Input.IsKeyPressed(KeyboardKey.Space)));
    }

    static void DrawWindowChromeGlyph(Gui gui, int kind, Rect rect)
    {
        var x = rect.X + (rect.W - 10) * 0.5f;
        var y = rect.Y + (rect.H - 10) * 0.5f;
        var color = gui.ControlStyle.Text;
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
                gui.DrawRect(new Rect(x, y + 2, 8, 8), gui.ControlStyle.Surface);
                gui.DrawRectBorder(new Rect(x, y + 2, 8, 8), color, 1);
                break;
            default:
                gui.DrawLine(new Vector2(x, y), new Vector2(x + 10, y + 10), color);
                gui.DrawLine(new Vector2(x + 10, y), new Vector2(x, y + 10), color);
                break;
        }
    }
}

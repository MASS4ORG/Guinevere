using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Application chrome dispatch, composition, overflow and pointer ownership.</summary>
public class AppBarTests
{
    sealed class Window(FrameHarness harness) : IWindowChromeCapability
    {
        /// <inheritdoc />
        public bool IsMaximized { get; private set; }
        /// <inheritdoc />
        public bool CanMove { get; set; } = true;
        /// <inheritdoc />
        public Vector2 Position { get; set; } = new(100, 100);
        /// <inheritdoc />
        /// <inheritdoc />
        public Vector2 PointerPosition => Position + harness.Input.MousePosition;
        /// <summary>Counts native decoration requests.</summary>
        public int DecorationCalls { get; private set; }
        /// <summary>Counts minimization requests.</summary>
        public int MinimizeCalls { get; private set; }
        /// <summary>Counts close requests.</summary>
        public int CloseRequests { get; private set; }
        /// <summary>Controls the simulated unsaved-work guard.</summary>
        public bool AllowClose { get; set; }
        /// <summary>Reports whether the guard allowed the last close request.</summary>
        public bool Closed { get; private set; }
        /// <inheritdoc />
        /// <summary>Counts native decoration requests.</summary>
        public void DrawWindowTitlebar(bool show) => DecorationCalls++;
        /// <inheritdoc />
        /// <summary>Counts minimization requests.</summary>
        public void Minimize() => MinimizeCalls++;
        /// <inheritdoc />
        /// <inheritdoc />
        public void Maximize() => IsMaximized = true;
        /// <inheritdoc />
        /// <inheritdoc />
        public void Restore() => IsMaximized = false;
        /// <inheritdoc />
        public void RequestClose()
        {
            CloseRequests++;
            Closed = AllowClose;
        }
    }

    static LayoutNode Bar(FrameHarness h) => h.Gui.RootNode!.Children[0];

    static void Draw(Gui gui) => gui.AppBar(bar => bar.Title("Studio"));

    /// <summary>Checks window commands and the unsaved-work close guard.</summary>
    [Fact]
    public void ControlsUseTheWindowCapabilityAndCloseRequestsCanBeVetoed()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        h.Frame(Draw);
        Assert.Equal(1, window.DecorationCalls);

        h.Click(Draw, Bar(h).Children[^3].Center);
        Assert.Equal(1, window.MinimizeCalls);
        h.Click(Draw, Bar(h).Children[^2].Center);
        Assert.True(window.IsMaximized);
        h.Frame(Draw);
        h.Click(Draw, Bar(h).Children[^2].Center);
        Assert.False(window.IsMaximized);

        h.Click(Draw, Bar(h).Children[^1].Center);
        Assert.Equal(1, window.CloseRequests);
        Assert.False(window.Closed);
        window.AllowClose = true;
        h.Click(Draw, Bar(h).Children[^1].Center);
        Assert.True(window.Closed);
        Assert.Equal(1, window.DecorationCalls);
    }

    /// <summary>Checks drag coordinates when moving the host changes local pointer positions.</summary>
    [Fact]
    public void DragUsesDesktopCoordinatesAndStopsWhenThePointerStops()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        h.Frame(Draw);
        var press = Bar(h).Children[0].Center;
        h.Input.MoveTo(press);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Draw);
        h.Input.MoveTo(press + new Vector2(40, 20));
        h.Frame(Draw);
        Assert.Equal(new Vector2(140, 120), window.Position);
        // Window movement changes local pointer coordinates without moving the desktop pointer.
        h.Input.MoveTo(press);
        h.Frame(Draw);
        h.Frame(Draw);
        Assert.Equal(new Vector2(140, 120), window.Position);
        h.Input.ReleaseButton(MouseButton.Left);
        h.Frame(Draw);
        h.Input.MoveTo(press + new Vector2(30, 10));
        h.Frame(Draw);
        Assert.Equal(new Vector2(140, 120), window.Position);
    }

    /// <summary>Checks double-click maximization and movement capability discovery.</summary>
    [Fact]
    public void DoubleClickTogglesMaximizeAndUnsupportedMovementStaysInert()
    {
        using var h = new FrameHarness();
        var window = new Window(h) { CanMove = false };
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        h.Frame(Draw);
        var point = Bar(h).Children[0].Center;
        h.Click(Draw, point);
        h.Click(Draw, point);
        Assert.True(window.IsMaximized);
        h.Input.MoveTo(point);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Draw);
        h.Input.MoveTo(point + new Vector2(20, 10));
        h.Frame(Draw);
        Assert.Equal(new Vector2(100, 100), window.Position);
    }

    /// <summary>Keeps custom control activation separate from window movement.</summary>
    [Fact]
    public void CustomContentStaysInteractiveAndHasSeparateDraggableSpace()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        var clicked = 0;
        void Content(Gui gui)
        {
            if (gui.Button("Search", 100, 36)) clicked++;
        }
        void Custom(Gui gui) => gui.AppBar(bar => bar
            .Leading(g => g.DrawText("Brand"), 60).Content(Content));
        h.Frame(Custom);
        var content = Bar(h).Children[1];
        h.Click(Custom, content.Children[0].Center);
        Assert.Equal(1, clicked);
        Assert.Equal(new Vector2(100, 100), window.Position);
        Assert.Equal(24, Bar(h).Children[2].Rect.W);
    }

    /// <summary>Checks action dispatch without native window controls.</summary>
    [Fact]
    public void HeadlessAndEmbeddedBarsRenderActionsWithoutNativeChrome()
    {
        using var h = new FrameHarness();
        var runs = 0;
        void Embedded(Gui gui) => gui.AppBar(bar => bar.Title("Embedded")
            .Action("Run", () => runs++)
            .Action("Off", () => runs++, enabled: false), windowControls: false);
        h.Frame(Embedded);
        Assert.Equal(3, Bar(h).Children.Count);
        h.Click(Embedded, Bar(h).Children[1].Center);
        Assert.Equal(1, runs);
        h.Click(Embedded, Bar(h).Children[2].Center);
        Assert.Equal(1, runs);
    }

    /// <summary>Checks keyboard access to overflow actions.</summary>
    [Fact]
    public void OverflowActionsRemainKeyboardAccessibleAndSkipDisabledItems()
    {
        using var h = new FrameHarness(width: 300);
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        var runs = 0;
        void Overflow(Gui gui) => gui.AppBar(bar => bar.Title("Narrow")
            .Action("Off", () => runs += 10, enabled: false)
            .Action("Run", () => runs++, shortcut: "Ctrl+R"));
        h.Frame(Overflow);
        h.Click(Overflow, Bar(h).Children[1].Center);
        h.Frame(Overflow);
        h.Input.PressKey(KeyboardKey.Down);
        h.Frame(Overflow);
        h.Input.ReleaseKey(KeyboardKey.Down);
        h.Input.PressKey(KeyboardKey.Enter);
        h.Frame(Overflow);
        Assert.Equal(1, runs);
    }
}

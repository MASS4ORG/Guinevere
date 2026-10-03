using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Application chrome, direct composition, movement and runtime decoration changes.</summary>
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
        public Vector2 PointerPosition => Position + harness.Input.MousePosition;
        /// <summary>The decoration requests sent to the host.</summary>
        public List<bool> Decorations { get; } = [];
        /// <summary>Counts minimization requests.</summary>
        public int MinimizeCalls { get; private set; }
        /// <summary>Counts close requests.</summary>
        public int CloseRequests { get; private set; }
        /// <summary>Controls the simulated unsaved-work guard.</summary>
        public bool AllowClose { get; set; }
        /// <summary>Reports whether the guard allowed the last close request.</summary>
        public bool Closed { get; private set; }
        /// <inheritdoc />
        public void DrawWindowTitlebar(bool show) => Decorations.Add(show);
        /// <inheritdoc />
        public void Minimize() => MinimizeCalls++;
        /// <inheritdoc />
        public void Maximize() => IsMaximized = true;
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
    static LayoutNode Content(FrameHarness h) => Bar(h).Children[0];

    static void Draw(Gui gui)
    {
        using (gui.AppBar()) gui.DrawText("Studio");
    }

    /// <summary>Checks window commands and the unsaved-work close guard.</summary>
    [Fact]
    public void ControlsUseTheWindowCapabilityAndCloseRequestsCanBeVetoed()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        h.Frame(Draw);
        Assert.Equal([false], window.Decorations);
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
        Assert.Single(window.Decorations);
    }

    /// <summary>Checks stable desktop coordinates after moving the host changes local pointer coordinates.</summary>
    [Fact]
    public void DragUsesDesktopCoordinatesAndStopsWhenThePointerStops()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        h.Frame(Draw);
        var press = Content(h).Center;
        h.Input.MoveTo(press);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Draw);
        h.Input.MoveTo(press + new Vector2(40, 20));
        h.Frame(Draw);
        Assert.Equal(new Vector2(140, 120), window.Position);
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

    /// <summary>Checks double-click maximization and movement while maximized.</summary>
    [Fact]
    public void DoubleClickMaximizesWithoutMovingTheMaximizedWindow()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        h.Frame(Draw);
        var point = Content(h).Center;
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

    /// <summary>Keeps native decorations when the backend cannot move the window.</summary>
    [Fact]
    public void UnsupportedMovementKeepsTheNativeTitlebar()
    {
        using var h = new FrameHarness();
        var window = new Window(h) { CanMove = false };
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        h.Frame(Draw);
        Assert.Equal([true], window.Decorations);
        Assert.Equal(4, Bar(h).Children.Count);
        h.Input.MoveTo(Content(h).Center);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Draw);
        h.Input.MoveTo(Content(h).Center + new Vector2(40, 10));
        h.Frame(Draw);
        Assert.Equal(new Vector2(100, 100), window.Position);
    }

    /// <summary>Retains working window buttons when movement is unavailable or native chrome is requested.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void WindowButtonsRemainAvailableWithNativeDecorations(bool canMove, bool native)
    {
        using var h = new FrameHarness();
        var window = new Window(h) { CanMove = canMove, AllowClose = true };
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        void Native(Gui gui)
        {
            using var bar = gui.AppBar(nativeTitlebar: native);
        }
        h.Frame(Native);
        Assert.Equal([true], window.Decorations);
        h.Click(Native, Bar(h).Children[^3].Center);
        Assert.Equal(1, window.MinimizeCalls);
        h.Click(Native, Bar(h).Children[^2].Center);
        Assert.True(window.IsMaximized);
        h.Frame(Native);
        h.Click(Native, Bar(h).Children[^2].Center);
        Assert.False(window.IsMaximized);
        h.Click(Native, Bar(h).Children[^1].Center);
        Assert.True(window.Closed);
    }

    /// <summary>Places repeated arbitrary widgets sequentially and makes passive images draggable.</summary>
    [Fact]
    public void ArbitraryImagesKeepTheirOrderAndMoveTheWindow()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        using var badge = Bitmap.FromPixels(new SKImageInfo(1, 1), [255, 255, 255, 255]);
        void Images(Gui gui)
        {
            using var bar = gui.AppBar();
            bar.Node.Gap(6);
            gui.Image(badge, width: 15, height: 15);
            gui.Image(badge, width: 15, height: 15);
            gui.Image(badge, width: 15, height: 15);
        }
        h.Frame(Images);
        var children = Content(h).Children;
        Assert.Equal(3, children.Count);
        Assert.True(children[0].Rect.X + children[0].Rect.W < children[1].Rect.X);
        Assert.True(children[1].Rect.X + children[1].Rect.W < children[2].Rect.X);
        h.Input.MoveTo(children[1].Center);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Images);
        h.Input.MoveTo(children[1].Center + new Vector2(25, 10));
        h.Frame(Images);
        Assert.Equal(new Vector2(125, 110), window.Position);
    }

    /// <summary>Preserves button activation and never turns a control press into window movement.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlsStayInteractiveAndBackgroundStartsANewDrag(bool shaped)
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        var clicked = 0;
        void Custom(Gui gui)
        {
            using var bar = gui.AppBar();
            using var group = gui.Node(100, 36).Enter();
            if (shaped)
            {
                if (gui.Pass == Pass.Pass2Render
                    && gui.GetInteractable(gui.CurrentNode.Rect.Position, Shape.Rectangle(100, 36)).OnClick())
                    clicked++;
            }
            else if (gui.Button("Search", 100, 36)) clicked++;
        }
        h.Frame(Custom);
        var control = Content(h).Children[0].Center;
        h.Input.MoveTo(control);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Custom);
        h.Input.MoveTo(new Vector2(200, 18));
        h.Frame(Custom);
        Assert.Equal(1, clicked);
        Assert.Equal(new Vector2(100, 100), window.Position);
        h.Input.ReleaseButton(MouseButton.Left);
        h.Frame(Custom);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Custom);
        h.Input.MoveTo(new Vector2(230, 28));
        h.Frame(Custom);
        Assert.Equal(new Vector2(130, 110), window.Position);
    }

    /// <summary>Preserves explicit pointer event handlers inside the bar.</summary>
    [Fact]
    public void EventControlsOwnTheirPointerGesture()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        var presses = 0;
        void Custom(Gui gui)
        {
            using var bar = gui.AppBar();
            using var control = gui.Node(100, 36).On<PointerDownEvent>(_ => presses++).Enter();
        }
        h.Frame(Custom);
        h.Input.MoveTo(Content(h).Children[0].Center);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Custom);
        h.Input.MoveTo(new Vector2(200, 18));
        h.Frame(Custom);
        Assert.Equal(1, presses);
        Assert.Equal(new Vector2(100, 100), window.Position);
    }

    /// <summary>Supports ordinary controls without a native window or while embedded in a host.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HeadlessAndEmbeddedBarsUseOrdinaryButtons(bool registerWindow)
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        if (registerWindow) h.Gui.Platform.Register<IWindowChromeCapability>(window);
        var runs = 0;
        void Embedded(Gui gui)
        {
            using var bar = gui.AppBar(windowControls: false);
            if (gui.Button("Run", 50, 36)) runs++;
            if (gui.Button("Off", 50, 36, enabled: false)) runs++;
        }
        h.Frame(Embedded);
        Assert.Single(Bar(h).Children);
        h.Click(Embedded, Content(h).Children[0].Center);
        Assert.Equal(1, runs);
        h.Click(Embedded, Content(h).Children[1].Center);
        Assert.Equal(1, runs);
        Assert.Empty(window.Decorations);
    }

    /// <summary>Switches decoration modes at runtime and restores chrome when management stops.</summary>
    [Fact]
    public void NativeTitlebarCanBeToggledAndManagementCanBeReleased()
    {
        using var h = new FrameHarness();
        var window = new Window(h);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        var native = false;
        var manage = true;
        void Toggle(Gui gui)
        {
            using var bar = gui.AppBar(windowControls: manage, nativeTitlebar: native);
            if (gui.Button("Decorations", 100, 36)) native = !native;
        }
        h.Frame(Toggle);
        Assert.Equal(4, Bar(h).Children.Count);
        h.Click(Toggle, Content(h).Children[0].Center);
        h.Frame(Toggle);
        Assert.Equal(4, Bar(h).Children.Count);
        h.Click(Toggle, Content(h).Children[0].Center);
        h.Frame(Toggle);
        Assert.Equal(4, Bar(h).Children.Count);
        manage = false;
        h.Frame(Toggle);
        Assert.Single(Bar(h).Children);
        Assert.Equal([false, true, false, true], window.Decorations);
    }
}

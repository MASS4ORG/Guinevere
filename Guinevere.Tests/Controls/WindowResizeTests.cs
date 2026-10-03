using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Window border gestures, cursor feedback, sizing constraints and chrome transitions.</summary>
public class WindowResizeTests
{
    sealed class Window(FrameHarness harness) : IWindowChromeCapability, IWindowResizeCapability
    {
        Vector2 _size = new(400, 300);

        /// <inheritdoc />
        public bool CanMove { get; set; } = true;
        /// <inheritdoc />
        public bool CanResize { get; set; } = true;
        /// <inheritdoc />
        public bool IsMaximized { get; set; }
        /// <inheritdoc />
        public Vector2 Position { get; set; } = new(100, 100);
        /// <inheritdoc />
        public Vector2 PointerPosition { get; set; }
        /// <inheritdoc />
        public Vector2 ClientSize
        {
            get => _size;
            set
            {
                _size = value;
                SizeChanges++;
                harness.Gui.SetScreenRect(value.X, value.Y);
            }
        }
        /// <summary>Counts native sizing requests.</summary>
        public int SizeChanges { get; private set; }
        /// <summary>Counts native close requests.</summary>
        public int CloseRequests { get; private set; }
        /// <inheritdoc />
        public void DrawWindowTitlebar(bool show) { }
        /// <inheritdoc />
        public void Minimize() { }
        /// <inheritdoc />
        public void Maximize() => IsMaximized = true;
        /// <inheritdoc />
        public void Restore() => IsMaximized = false;
        /// <inheritdoc />
        public void RequestClose() => CloseRequests++;
    }

    static Window Register(FrameHarness h, bool resize = true)
    {
        var window = new Window(h);
        h.Gui.Platform.Register<ICursorCapability>(h.Input);
        h.Gui.Platform.Register<IWindowChromeCapability>(window);
        if (resize) h.Gui.Platform.Register<IWindowResizeCapability>(window);
        return window;
    }

    static void Move(FrameHarness h, Window window, Vector2 desktop)
    {
        window.PointerPosition = desktop;
        h.Input.MoveTo(desktop - window.Position);
    }

    static void Draw(Gui gui)
    {
        using (gui.AppBar(resizable: true)) gui.DrawText("Studio");
        gui.Button("Content", gui.ScreenRect.W, gui.ScreenRect.H - 36);
    }

    /// <summary>Resizes each edge and corner while keeping the opposite edges anchored.</summary>
    [Theory]
    [InlineData(2, 150, 140, 100, 360, 300, PointerCursor.ResizeHorizontal)]
    [InlineData(398, 150, 100, 100, 440, 300, PointerCursor.ResizeHorizontal)]
    [InlineData(200, 2, 100, 120, 400, 280, PointerCursor.ResizeVertical)]
    [InlineData(200, 298, 100, 100, 400, 320, PointerCursor.ResizeVertical)]
    [InlineData(2, 2, 140, 120, 360, 280, PointerCursor.ResizeDiagonalNorthWestSouthEast)]
    [InlineData(398, 298, 100, 100, 440, 320, PointerCursor.ResizeDiagonalNorthWestSouthEast)]
    [InlineData(398, 2, 100, 120, 440, 280, PointerCursor.ResizeDiagonalNorthEastSouthWest)]
    [InlineData(2, 298, 140, 100, 360, 320, PointerCursor.ResizeDiagonalNorthEastSouthWest)]
    public void BordersAndCornersResizeWithoutDrifting(float x, float y, float left, float top,
        float width, float height, PointerCursor cursor)
    {
        using var h = new FrameHarness();
        var window = Register(h);
        var desktop = window.Position + new Vector2(x, y);
        h.Frame(Draw);
        Move(h, window, desktop);
        h.Frame(Draw);
        Assert.Equal(cursor, h.Input.Cursor);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Draw);
        Assert.True(h.Gui.IsPointerCaptured);
        desktop += new Vector2(40, 20);
        Move(h, window, desktop);
        h.Frame(Draw);
        Assert.Equal(new Vector2(left, top), window.Position);
        Assert.Equal(new Vector2(width, height), window.ClientSize);
        Move(h, window, desktop);
        h.Frame(Draw);
        h.Frame(Draw);
        Assert.Equal(1, window.SizeChanges);
        Assert.Equal(new Vector2(left, top), window.Position);
        Assert.Equal(cursor, h.Input.Cursor);
        h.Input.ReleaseButton(MouseButton.Left);
        h.Frame(Draw);
        Move(h, window, desktop + new Vector2(20, 10));
        h.Frame(Draw);
        Assert.False(h.Gui.IsPointerCaptured);
        Assert.Equal(1, window.SizeChanges);
        Assert.Equal(0, window.CloseRequests);
    }

    /// <summary>Clamps both axes and preserves the original opposite corner after crossing the minimum.</summary>
    [Theory]
    [InlineData(2, 2, 1000, 1000, 340, 290)]
    [InlineData(398, 298, -1000, -1000, 100, 100)]
    public void MinimumSizeKeepsTheOppositeCornerFixed(float x, float y, float dx, float dy,
        float left, float top)
    {
        using var h = new FrameHarness();
        var window = Register(h);
        void Small(Gui gui)
        {
            using var bar = gui.AppBar(resizable: true, minimumWindowSize: new Vector2(159.5f, 109.5f));
        }
        h.Frame(Small);
        var desktop = window.Position + new Vector2(x, y);
        Move(h, window, desktop);
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Small);
        Move(h, window, desktop + new Vector2(dx, dy));
        h.Frame(Small);
        Assert.Equal(new Vector2(160, 110), window.ClientSize);
        Assert.Equal(new Vector2(left, top), window.Position);
        Move(h, window, desktop);
        h.Frame(Small);
        Assert.Equal(new Vector2(400, 300), window.ClientSize);
        Assert.Equal(new Vector2(100, 100), window.Position);
    }

    /// <summary>Leaves sizing with the OS or host when custom resizing is disabled or unavailable.</summary>
    [Theory]
    [InlineData(false, false, false, true, true, true)]
    [InlineData(true, true, false, true, true, true)]
    [InlineData(true, false, true, true, true, true)]
    [InlineData(true, false, false, false, true, true)]
    [InlineData(true, false, false, true, false, true)]
    [InlineData(true, false, false, true, true, false)]
    public void DisabledOrUnsupportedChromeHasNoResizeHandles(bool enabled, bool native, bool maximized,
        bool canMove, bool canResize, bool registerResize)
    {
        using var h = new FrameHarness();
        var window = Register(h, registerResize);
        window.IsMaximized = maximized;
        window.CanMove = canMove;
        window.CanResize = canResize;
        void Disabled(Gui gui)
        {
            using var bar = gui.AppBar(resizable: enabled, nativeTitlebar: native);
        }
        h.Frame(Disabled);
        Assert.Equal(4, h.Gui.RootNode!.Children[0].Children.Count);
        Move(h, window, new Vector2(498, 398));
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Disabled);
        Move(h, window, new Vector2(520, 430));
        h.Frame(Disabled);
        Assert.Equal(0, window.SizeChanges);
        Assert.Equal(PointerCursor.Default, h.Input.Cursor);
    }

    /// <summary>Prevents a gesture captured by content from becoming a resize when it crosses a border.</summary>
    [Fact]
    public void ContentCaptureCannotTurnIntoBorderResizing()
    {
        using var h = new FrameHarness();
        var window = Register(h);
        void Content(Gui gui)
        {
            using (gui.AppBar(resizable: true)) gui.DrawText("Studio");
            using var body = gui.Node(400, 264).Enter();
            if (gui.Pass == Pass.Pass2Render) gui.GetInteractable().OnDrag(out _);
        }
        h.Frame(Content);
        Move(h, window, new Vector2(300, 250));
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Content);
        var owner = h.Gui.PointerCapture;
        Move(h, window, new Vector2(498, 398));
        h.Frame(Content);
        Assert.Equal(owner, h.Gui.PointerCapture);
        Assert.Equal(0, window.SizeChanges);
        Assert.Equal(PointerCursor.Default, h.Input.Cursor);
    }

    /// <summary>Changes resize availability between frames without changing the tree during a render pass.</summary>
    [Fact]
    public void RuntimeFlagsAndMaximizationSwitchTheResizeHandles()
    {
        using var h = new FrameHarness();
        var window = Register(h);
        var native = false;
        var resize = true;
        void Toggle(Gui gui)
        {
            using var bar = gui.AppBar(resizable: resize, nativeTitlebar: native);
        }
        h.Frame(Toggle);
        Assert.Equal(12, h.Gui.RootNode!.Children[0].Children.Count);
        native = true;
        h.Frame(Toggle);
        Assert.Equal(4, h.Gui.RootNode.Children[0].Children.Count);
        native = false;
        window.Maximize();
        h.Frame(Toggle);
        Assert.Equal(4, h.Gui.RootNode.Children[0].Children.Count);
        window.Restore();
        h.Frame(Toggle);
        Assert.Equal(12, h.Gui.RootNode.Children[0].Children.Count);
        resize = false;
        h.Frame(Toggle);
        Assert.Equal(4, h.Gui.RootNode.Children[0].Children.Count);
    }

    /// <summary>Rejects invalid minimum sizes before creating resize gestures.</summary>
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, -1)]
    [InlineData(float.NaN, 100)]
    [InlineData(100, float.PositiveInfinity)]
    public void MinimumSizeMustBeFiniteAndPositive(float width, float height)
    {
        using var h = new FrameHarness();
        Assert.Throws<ArgumentOutOfRangeException>(() => h.Frame(gui =>
        {
            using var bar = gui.AppBar(resizable: true, minimumWindowSize: new Vector2(width, height));
        }));
    }

    /// <summary>Keeps handles on the viewport perimeter even when the bar is nested in clipped content.</summary>
    [Fact]
    public void WindowHandlesEscapeParentClippingAndBlockContent()
    {
        using var h = new FrameHarness();
        var window = Register(h);
        var clicks = 0;
        void Clipped(Gui gui)
        {
            using (gui.Node(400, 36).Enter())
            {
                gui.SetClipped(true);
                using var bar = gui.AppBar(resizable: true);
            }
            if (gui.Button("Content", 400, 264)) clicks++;
        }
        h.Frame(Clipped);
        Move(h, window, new Vector2(498, 398));
        h.Input.PressButton(MouseButton.Left);
        h.Frame(Clipped);
        Move(h, window, new Vector2(520, 430));
        h.Frame(Clipped);
        Assert.Equal(new Vector2(422, 332), window.ClientSize);
        Assert.Equal(0, clicks);
    }
}

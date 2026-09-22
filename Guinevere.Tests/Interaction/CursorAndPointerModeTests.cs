using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

/// <summary>
/// The cursor shape comes from a per-frame request, else the node holding the pointer, else the topmost node
/// under it; pointer modes map onto the integration's visible/locked/warp primitives.
/// </summary>
public class CursorAndPointerModeTests
{
    const int Size = 200;

    sealed class Harness
    {
        readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Size, Size));

        public Harness()
        {
            Gui = new TestableGui { Input = Input };
            Gui.SetScreenRect(Size, Size);
            Gui.Platform.Register<ICursorCapability>(Input);
            Gui.Platform.Register<IPointerCapability>(Input);
        }

        public ScriptedInputHandler Input { get; } = new();
        public TestableGui Gui { get; }

        public void Frame(Action<Gui> draw)
        {
            Gui.SetStage(Pass.Pass1Build);
            Gui.BeginFrame(_surface.Canvas);
            draw(Gui);
            Gui.CalculateLayout();
            Gui.SetStage(Pass.Pass2Render);
            draw(Gui);
            Gui.EndFrame();
            Input.NewFrame();
        }
    }

    static void Panels(Gui gui)
    {
        using (gui.Node(Size, 100, "editor").Direction(Axis.Horizontal).Cursor(PointerCursor.Text).Enter())
        {
            using (gui.Node(100, 100, "text").Enter()) { }
            using (gui.Node(100, 100, "link").Cursor(PointerCursor.Hand).Enter()) { }
        }
    }

    [Fact]
    public void CursorComesFromTheNearestNodeThatSetsOne()
    {
        var harness = new Harness();

        harness.Input.MoveTo(50, 50);
        harness.Frame(Panels);
        Assert.Equal(PointerCursor.Text, harness.Input.Cursor);

        harness.Input.MoveTo(150, 50);
        harness.Frame(Panels);
        Assert.Equal(PointerCursor.Hand, harness.Input.Cursor);

        harness.Input.MoveTo(50, 150);
        harness.Frame(Panels);
        Assert.Equal(PointerCursor.Default, harness.Input.Cursor);
    }

    [Fact]
    public void TheGestureOwnerKeepsItsCursorWhileThePointerWanders()
    {
        var harness = new Harness();
        void Draw(Gui gui)
        {
            using (gui.Node(Size, 100, "row").Direction(Axis.Horizontal).Enter())
            {
                using (gui.Node(100, 100, "splitter").Cursor(PointerCursor.ResizeHorizontal).Enter())
                    if (gui.Pass == Pass.Pass2Render)
                        gui.GetInteractable().OnDrag(out _);
                using (gui.Node(100, 100, "text").Cursor(PointerCursor.Text).Enter()) { }
            }
        }

        harness.Input.MoveTo(50, 50);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(150, 50);
        harness.Frame(Draw);
        Assert.Equal(PointerCursor.ResizeHorizontal, harness.Input.Cursor);

        harness.Input.ReleaseButton();
        harness.Frame(Draw);
        Assert.Equal(PointerCursor.Text, harness.Input.Cursor);
    }

    [Fact]
    public void ARequestOverridesNodeCursorsForOneFrame()
    {
        var harness = new Harness();
        harness.Input.MoveTo(50, 50);

        harness.Frame(gui =>
        {
            Panels(gui);
            gui.RequestCursor(PointerCursor.NotAllowed);
        });
        Assert.Equal(PointerCursor.NotAllowed, harness.Input.Cursor);

        harness.Frame(Panels);
        Assert.Equal(PointerCursor.Text, harness.Input.Cursor);
    }

    [Fact]
    public void TheCursorIsOnlyPushedWhenItChanges()
    {
        var harness = new Harness();
        var cursor = Substitute.For<ICursorCapability>();
        harness.Gui.Platform.Register(cursor);
        harness.Input.MoveTo(50, 50);

        harness.Frame(Panels);
        harness.Frame(Panels);
        harness.Frame(Panels);

        cursor.Received(1).Cursor = PointerCursor.Text;
    }

    [Theory]
    [InlineData(PointerMode.Normal, true, false)]
    [InlineData(PointerMode.Hidden, false, false)]
    [InlineData(PointerMode.Relative, false, true)]
    [InlineData(PointerMode.Wrapped, true, false)]
    public void ModesMapOntoVisibleAndLocked(PointerMode mode, bool visible, bool locked)
    {
        var harness = new Harness();

        harness.Frame(gui => gui.RequestPointerMode(mode));

        Assert.Equal(mode, harness.Gui.PointerMode);
        Assert.Equal(visible, harness.Input.Visible);
        Assert.Equal(locked, harness.Input.Locked);
    }

    [Fact]
    public void ThePointerReturnsToNormalWhenNobodyAsks()
    {
        var harness = new Harness();

        harness.Frame(gui => gui.RequestPointerMode(PointerMode.Relative));
        harness.Frame(_ => { });

        Assert.Equal(PointerMode.Normal, harness.Gui.PointerMode);
        Assert.True(harness.Input.Visible);
        Assert.False(harness.Input.Locked);
    }

    [Fact]
    public void WrappingKeepsTheDragTotalContinuous()
    {
        var harness = new Harness();
        var total = Vector2.Zero;
        void Draw(Gui gui)
        {
            using (gui.Node(Size, Size, "scrub").Enter())
                if (gui.Pass == Pass.Pass2Render && gui.GetInteractable().OnDrag(out var drag))
                {
                    total = drag.TotalDelta;
                    gui.RequestPointerMode(PointerMode.Wrapped);
                }
        }

        harness.Input.MoveTo(100, 100);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(Size - 1, 100);
        harness.Frame(Draw);
        Assert.Equal(new Vector2(99, 0), total);
        Assert.Equal(1, harness.Input.WarpCount);
        Assert.Equal(new Vector2(1, 100), harness.Input.MousePosition);

        // Five more pixels to the right after the warp: the total keeps counting from 99.
        harness.Input.MoveTo(6, 100);
        harness.Frame(Draw);
        Assert.Equal(new Vector2(104, 0), total);
    }

    [Fact]
    public void WithoutCapabilitiesRequestsAreHarmless()
    {
        var gui = new TestableGui { Input = new ScriptedInputHandler() };
        gui.SetScreenRect(Size, Size);
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size));

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        gui.RequestCursor(PointerCursor.Hand);
        gui.RequestPointerMode(PointerMode.Wrapped);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        gui.EndFrame();

        Assert.Equal(PointerMode.Normal, gui.PointerMode);
    }
}

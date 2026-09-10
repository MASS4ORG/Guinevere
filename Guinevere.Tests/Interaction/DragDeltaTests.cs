using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Interaction;

/// <summary>
/// Covers per-frame pointer movement. Integrations disagree about what
/// <see cref="IInputHandler.PrevMousePosition"/> means — most update it from the pointer-move event,
/// so once the pointer stops it keeps reporting the last movement forever.
/// </summary>
public class DragDeltaTests
{
    private const int Size = 200;

    /// <summary>
    /// Drives frames the way the SilkNET integration does: <see cref="IInputHandler.MousePosition"/>
    /// is current, but <see cref="IInputHandler.PrevMousePosition"/> is only updated when the pointer
    /// actually moves, so it goes stale the moment it stops.
    /// </summary>
    private sealed class StaleGuiWindow
    {
        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(Size, Size));
        private readonly IInputHandler _input = Substitute.For<IInputHandler>();
        private readonly TestableGui _gui;
        private Vector2 _position;
        private Vector2 _previous;

        public StaleGuiWindow(Vector2 start)
        {
            _position = _previous = start;
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(Size, Size);
        }

        public DragArgs LastArgs { get; private set; }
        public float Fraction = 0.5f;

        public void MoveTo(Vector2 position)
        {
            _previous = _position;
            _position = position;
        }

        public void Frame(bool down, bool pressed = false)
        {
            _input.MousePosition.Returns(_position);
            _input.PrevMousePosition.Returns(_previous);
            _input.IsMouseButtonDown(MouseButton.Left).Returns(down);
            _input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);

            var fraction = Fraction;

            void Draw()
            {
                using (_gui.Node(Size, Size, "host").Direction(Axis.Horizontal).Enter())
                {
                    using (_gui.Node(97, Size, "a").Enter())
                    {
                    }

                    _gui.Splitter(ref fraction, Axis.Horizontal, min: 0f);

                    using (_gui.Node(97, Size, "b").Enter())
                    {
                        if (_gui.Pass == Pass.Pass2Render && _gui.GetInteractable().OnDrag(out var args))
                            LastArgs = args;
                    }
                }
            }

            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.EndFrame();

            Fraction = fraction;
        }
    }

    [Fact]
    public void FrameDeltaIsZeroOnceThePointerStopsEvenIfTheIntegrationSaysOtherwise()
    {
        var window = new StaleGuiWindow(new Vector2(150, 100));

        window.Frame(down: true, pressed: true);
        window.MoveTo(new Vector2(160, 100));
        window.Frame(down: true);
        Assert.Equal(new Vector2(10, 0), window.LastArgs.FrameDelta);

        // The pointer stops. PrevMousePosition stays where it was, so a delta taken from it would
        // still read (10, 0) every frame from here on.
        window.Frame(down: true);
        Assert.Equal(Vector2.Zero, window.LastArgs.FrameDelta);

        window.Frame(down: true);
        Assert.Equal(Vector2.Zero, window.LastArgs.FrameDelta);
    }

    [Fact]
    public void ASplitterStopsMovingWhenThePointerStops()
    {
        // The splitter sits at the middle of a 200px host, so the press lands on it.
        var window = new StaleGuiWindow(new Vector2(100, 100));

        window.Frame(down: true, pressed: true);
        window.MoveTo(new Vector2(120, 100));
        window.Frame(down: true);

        var afterMove = window.Fraction;
        Assert.True(afterMove > 0.5f, $"the splitter should have followed the pointer, got {afterMove}");

        window.Frame(down: true);
        window.Frame(down: true);

        Assert.Equal(afterMove, window.Fraction, 4);
    }

    [Fact]
    public void ASplitterTracksTheTotalTravelRatherThanASumOfFrames()
    {
        var window = new StaleGuiWindow(new Vector2(100, 100));

        window.Frame(down: true, pressed: true);
        var start = window.Fraction;

        window.MoveTo(new Vector2(110, 100));
        window.Frame(down: true);
        window.MoveTo(new Vector2(130, 100));
        window.Frame(down: true);

        // 30px of travel across a 200px track, wherever the frames landed in between.
        Assert.Equal(start + 30f / Size, window.Fraction, 3);
    }
}

using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the reference slot an inspector builds on: the buttons inside the box report one action per
/// frame, and a payload dropped on the slot reaches the caller even though the drag only settles at
/// the frame boundary.
/// </summary>
public class ObjectFieldTests
{
    private static readonly Font TestFont = Font.FromFamilyName("serif");

    private sealed class Harness
    {
        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(400, 60));
        private readonly IInputHandler _input = Substitute.For<IInputHandler>();
        private readonly TestableGui _gui;

        public Harness()
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(400, 60);

            _input.GetTypedCharacters().Returns(string.Empty);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        }

        public string Id { get; } = $"slot/{Guid.NewGuid():N}";

        /// <summary>Runs one frame and reports what the slot saw.</summary>
        public ObjectFieldResult Frame(Vector2? mouse = null, bool pressed = false, bool down = false,
            Action<Gui>? before = null)
        {
            var at = mouse ?? new Vector2(-100, -100);
            _input.MousePosition.Returns(at);
            _input.PrevMousePosition.Returns(at);
            _input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(call => pressed && call.Arg<MouseButton>() == MouseButton.Left);
            _input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(call => down && call.Arg<MouseButton>() == MouseButton.Left);

            var result = default(ObjectFieldResult);

            void Draw()
            {
                before?.Invoke(_gui);
                var outcome = _gui.ObjectField("Cube", Id, isEmpty: false);
                if (outcome.Action != ObjectFieldAction.None) result = outcome;
            }

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas, TestFont);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();

            return result;
        }
    }

    /// <summary>The pick button opens the picker once, not once per pass.</summary>
    [Fact]
    public void ThePickButtonReportsOneActionPerFrame()
    {
        var h = new Harness();
        var toggles = 0;

        h.Frame(new Vector2(200, 10));
        if (h.Frame(new Vector2(370, 10), pressed: true).Action == ObjectFieldAction.Pick) toggles++;

        Assert.Equal(1, toggles);
    }

    /// <summary>
    /// A drag settles in <c>EndFrame</c>, after the slot has already returned its result, so the
    /// payload is parked and reported on the next frame instead of being dropped on the floor.
    /// </summary>
    [Fact]
    public void ADroppedPayloadIsReportedOnTheFollowingFrame()
    {
        var h = new Harness();
        var over = new Vector2(200, 10);

        h.Frame(over);
        h.Frame(over, down: true, before: gui => gui.BeginDrag("source", "payload"));

        // The release frame: the drop is queued here and resolved when the frame ends.
        Assert.Equal(ObjectFieldAction.None, h.Frame(over).Action);

        var settled = h.Frame(over);
        Assert.Equal(ObjectFieldAction.Drop, settled.Action);
        Assert.Equal("payload", settled.Payload);
    }
}

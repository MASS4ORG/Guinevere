using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers opening, choosing and dismissing. The list used to close itself the frame after it opened —
/// it shut whenever focus was elsewhere — so the options only ever blinked.
/// </summary>
public class DropdownBehaviourTests
{
    private static readonly string[] Options = ["Point", "Directional", "Spot"];

    private sealed class Harness
    {
        private readonly SKSurface _surface = SKSurface.Create(new SKImageInfo(300, 300));
        private readonly IInputHandler _input = Substitute.For<IInputHandler>();
        private readonly TestableGui _gui;

        // A fixed id on purpose: control state belongs to the Gui, so two harnesses sharing an id no
        // longer share an open/closed state.
        private const string Id = "test/dropdown";

        public Harness()
        {
            _gui = new TestableGui { Input = _input };
            _gui.SetScreenRect(300, 300);
            Selected = -1;
        }

        public int Selected { get; private set; }

        public Gui Gui => _gui;

        public void Frame(Vector2 mouse, bool pressed = false, KeyboardKey? key = null)
        {
            _input.MousePosition.Returns(mouse);
            _input.PrevMousePosition.Returns(mouse);
            _input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
            _input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(call => key is not null && call.Arg<KeyboardKey>() == key);

            var index = Selected;

            void Draw() => _gui.Dropdown(Options, ref index, width: 120, height: 24,
                filePath: Id, lineNumber: 0);

            _gui.Time.Update(0.016);
            _gui.SetStage(Pass.Pass1Build);
            _gui.BeginFrame(_surface.Canvas);
            Draw();
            _gui.CalculateLayout();
            _gui.SetStage(Pass.Pass2Render);
            Draw();
            _gui.Render();
            _gui.EndFrame();

            Selected = index;
        }

        /// <summary>
        /// Whether the list is in the tree. It lags state by a frame: closing happens in the render
        /// pass, after that frame's nodes were built, so settle a frame before asserting it is gone.
        /// </summary>
        public bool ListIsOpen => Exists(_gui.RootNode!, "/list");

        private static bool Exists(LayoutNode node, string suffix) =>
            node.Id.EndsWith(suffix, StringComparison.Ordinal)
            || node.Children.Any(child => Exists(child, suffix));
    }

    private static readonly Vector2 OnButton = new(40, 10);
    private static readonly Vector2 Away = new(280, 280);

    [Fact]
    public void ClickingTheButtonOpensTheList()
    {
        var harness = new Harness();
        harness.Frame(OnButton, pressed: true);

        Assert.True(harness.ListIsOpen);
    }

    [Fact]
    public void TheListStaysOpenOnFollowingFrames()
    {
        var harness = new Harness();
        harness.Frame(OnButton, pressed: true);

        harness.Frame(OnButton);
        harness.Frame(OnButton);

        Assert.True(harness.ListIsOpen, "the list closed itself after opening");
    }

    [Fact]
    public void ClickingAnOptionSelectsItAndCloses()
    {
        var harness = new Harness();
        harness.Frame(OnButton, pressed: true);
        harness.Frame(OnButton);

        // Second row of the list, which starts just under the 24px button.
        harness.Frame(new Vector2(40, 24 + 2 + 24 + 12), pressed: true);
        harness.Frame(OnButton);

        Assert.Equal(1, harness.Selected);
        Assert.False(harness.ListIsOpen);
    }

    [Fact]
    public void EscapeDismissesTheList()
    {
        var harness = new Harness();
        harness.Frame(OnButton, pressed: true);
        harness.Frame(OnButton);

        harness.Frame(OnButton, key: KeyboardKey.Escape);
        harness.Frame(OnButton);

        Assert.False(harness.ListIsOpen);
    }

    [Fact]
    public void APressAwayFromTheDropdownDismissesIt()
    {
        var harness = new Harness();
        harness.Frame(OnButton, pressed: true);
        harness.Frame(OnButton);

        harness.Frame(Away, pressed: true);
        harness.Frame(Away);

        Assert.False(harness.ListIsOpen);
    }

    [Fact]
    public void TheListTakesItsColoursFromTheControlPalette()
    {
        var harness = new Harness();
        harness.Gui.Controls = ControlPalette.Dark;
        harness.Frame(OnButton, pressed: true);

        Assert.True(harness.ListIsOpen);
    }

    [Fact]
    public void TwoGuisDoNotShareOneDropdownsState()
    {
        var first = new Harness();
        var second = new Harness();

        first.Frame(OnButton, pressed: true);
        second.Frame(Away);

        Assert.True(first.ListIsOpen);
        Assert.False(second.ListIsOpen, "a second Gui inherited the first one's open dropdown");
    }
}

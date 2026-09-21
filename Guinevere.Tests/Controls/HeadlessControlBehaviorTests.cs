using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

public class HeadlessControlBehaviorTests
{
    const int Width = 240;
    const int Height = 120;

    [Fact]
    public void VisualCallbackReceivesTheSameStateInBothPasses()
    {
        var gui = CreateGui();
        var states = new List<ControlVisualState>();

        Frame(gui, NoInput(), () => Control(gui, states));
        var node = gui.RootNode!.Children.Single();
        Frame(gui, MouseAt(node, pressed: false), () => Control(gui, states));
        states.Clear();

        Frame(gui, NoInput(), () => Control(gui, states));

        Assert.Equal(2, states.Count);
        Assert.Equal(states[0], states[1]);
        Assert.True(states[0].HasFlag(ControlVisualState.Hovered));
    }

    [Fact]
    public void PointerActivationIsDeliveredInTheNextLayoutPass()
    {
        var gui = CreateGui();
        var activatedByPass = new List<(Pass Pass, bool Activated)>();
        Frame(gui, NoInput(), () => Press(gui, activatedByPass));
        var node = gui.RootNode!.Children.Single();

        activatedByPass.Clear();
        Frame(gui, MouseAt(node, pressed: true), () => Press(gui, activatedByPass));
        Assert.All(activatedByPass, entry => Assert.False(entry.Activated));

        activatedByPass.Clear();
        Frame(gui, NoInput(), () => Press(gui, activatedByPass));
        Assert.Equal([(Pass.Pass1Build, true), (Pass.Pass2Render, false)], activatedByPass);
    }

    [Fact]
    public void DisabledToggleComposesStateAndIgnoresInput()
    {
        var gui = CreateGui();
        ControlBehaviorResult layout = default;
        void Draw()
        {
            using (gui.Node(100, 30, "toggle").Enter())
            {
                var result = gui.Toggleable(true,
                    new ControlBehaviorOptions(Enabled: false, Role: ControlRole.Switch));
                if (gui.Pass == Pass.Pass1Build) layout = result;
            }
        }

        Frame(gui, NoInput(), Draw);
        var node = gui.RootNode!.Children.Single();
        Frame(gui, MouseAt(node, pressed: true), Draw);
        Frame(gui, NoInput(), Draw);

        Assert.False(layout.Activated);
        Assert.True(layout.Is(ControlVisualState.Disabled));
        Assert.True(layout.Is(ControlVisualState.Checked));
    }

    [Fact]
    public void SemanticsCapabilityReceivesRoleLabelBoundsAndState()
    {
        var gui = CreateGui();
        var sink = new RecordingSemanticsSink();
        gui.ControlSemantics = sink;

        Frame(gui, NoInput(), () =>
        {
            using (gui.Node(100, 30, "save").Enter())
                gui.Pressable(new ControlBehaviorOptions(Role: ControlRole.Button, Label: "Save"));
        });

        var semantics = Assert.Single(sink.Items);
        Assert.Equal("save", semantics.Id);
        Assert.Equal(ControlRole.Button, semantics.Role);
        Assert.Equal("Save", semantics.Label);
        Assert.Equal(100, semantics.Bounds.W);
        Assert.Equal(30, semantics.Bounds.H);
    }

    [Fact]
    public void FocusedControlAcceptsRoutedControllerActivation()
    {
        var gui = CreateGui();
        var activations = new List<(Pass Pass, bool Activated)>();
        Frame(gui, NoInput(), () => Press(gui, activations));
        gui.RequestFocus("control");
        gui.ControlActivation = new ActivationSource("control");

        activations.Clear();
        Frame(gui, NoInput(), () => Press(gui, activations));
        activations.Clear();
        gui.ControlActivation = null;
        Frame(gui, NoInput(), () => Press(gui, activations));

        Assert.Equal([(Pass.Pass1Build, true), (Pass.Pass2Render, false)], activations);
    }

    static void Control(Gui gui, ICollection<ControlVisualState> states)
    {
        using (gui.Node(100, 30, "control").Enter())
            gui.Pressable(render: states.Add);
    }

    static void Press(Gui gui, ICollection<(Pass, bool)> activations)
    {
        using (gui.Node(100, 30, "control").Enter())
        {
            var result = gui.Pressable();
            activations.Add((gui.Pass, result.Activated));
        }
    }

    static Gui CreateGui()
    {
        var gui = new TestableGui { Input = NoInput() };
        gui.SetScreenRect(Width, Height);
        return gui;
    }

    static void Frame(Gui gui, IInputHandler input, Action draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        gui.Input = input;
        gui.Time.Update(0.016);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw();
        gui.Render();
        gui.EndFrame();
    }

    static IInputHandler NoInput()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        return input;
    }

    static IInputHandler MouseAt(LayoutNode node, bool pressed)
    {
        var input = NoInput();
        input.MousePosition.Returns(new Vector2(node.Rect.X + 5, node.Rect.Y + 5));
        input.IsMouseButtonPressed(MouseButton.Left).Returns(pressed);
        input.IsMouseButtonDown(MouseButton.Left).Returns(pressed);
        return input;
    }

    sealed class RecordingSemanticsSink : IControlSemanticsSink
    {
        public List<ControlSemantics> Items { get; } = [];
        public void Publish(ControlSemantics semantics) => Items.Add(semantics);
    }

    sealed class ActivationSource(string target) : IControlActivationSource
    {
        public bool IsActivated(string controlId) => controlId == target;
    }
}

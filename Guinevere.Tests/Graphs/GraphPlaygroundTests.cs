using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Graphs;

public sealed class GraphPlaygroundTests
{
    [Fact]
    public void ScenarioTabsPublishSemanticsAndSupportKeyboardNavigation()
    {
        using var harness = new FrameHarness(1000, 700);
        var scene = new GraphPlayground { ShowPendingScenarios = true };
        var sink = new SemanticsSink();
        harness.Gui.ControlSemantics = sink;
        void Draw(Gui gui)
        {
            if (gui.Pass == Pass.Pass1Build)
            {
                sink.Controls.Clear();
            }
            scene.Draw(gui);
        }
        harness.Frame(Draw);
        harness.Frame(Draw);
        var tabs = sink.Controls.Where(static control => control.Role == ControlRole.Tab).ToArray();
        Assert.Equal(["Sandbox", "Running flow", "Organizers"], tabs.Select(static tab => tab.Label));
        Assert.True(tabs[0].State.HasFlag(ControlVisualState.Selected));
        Focus(harness, Draw, "scenario-0");
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Right);
        Assert.Equal(1, scene.Scenario);
        Assert.Equal("scenario-1", harness.Gui.Focus.CurrentFocusedId);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.End);
        Assert.Equal(2, scene.Scenario);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Home);
        Assert.Equal(0, scene.Scenario);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Left);
        Assert.Equal(2, scene.Scenario);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.F10);
        Assert.Equal(2, scene.Scenario);
        Focus(harness, Draw, "scenario-1");
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Enter);
        harness.Frame(Draw);
        Assert.Equal(1, scene.Scenario);
        Assert.Contains(sink.Controls, static control => control.Id == "flow-canvas");
        Focus(harness, Draw, "scenario-2");
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Enter);
        harness.Frame(Draw);
        Assert.Equal(2, scene.Scenario);
        Focus(harness, Draw, "open-sandbox");
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Enter);
        harness.Frame(Draw);
        Assert.Equal(0, scene.Scenario);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SelectScenario(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SelectScenario(3));
    }

    [Fact]
    public void ToolbarAndViewMovementAreAvailableWithoutAPointer()
    {
        using var harness = new FrameHarness(1000, 700);
        var scene = new GraphPlayground();
        Action<Gui> draw = scene.Draw;
        harness.Frame(draw);
        harness.Frame(draw);
        Focus(harness, draw, "add-node");
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Focus(harness, draw, "create-" + ArithmeticGraph.Number.Id);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Assert.Equal(5, scene.Sandbox.Nodes.Count);
        Focus(harness, draw, "frame-all");
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Space);
        harness.Frame(draw);
        Assert.True(scene.Sandbox.Canvas.VisibleBounds.Contains(scene.Sandbox.Nodes[0].Bounds));
        scene.Sandbox.Canvas.Navigation.ClearSelection();
        Focus(harness, draw, "sandbox-canvas");
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Right);
        var key = scene.Sandbox.Canvas.Navigation.FocusedKey;
        var before = scene.Sandbox.Nodes.Single(card => card.Key == key).Bounds;
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Right, KeyboardKey.LeftShift);
        var after = scene.Sandbox.Nodes.Single(card => card.Key == key).Bounds;
        Assert.Equal(before.X + 16, after.X);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Left, KeyboardKey.LeftShift);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Up, KeyboardKey.LeftShift);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Down, KeyboardKey.LeftShift);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.F10, KeyboardKey.LeftShift);
        Assert.Equal(before, scene.Sandbox.Nodes.Single(card => card.Key == key).Bounds);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Space);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Delete);
        Assert.Equal(4, scene.Sandbox.Nodes.Count);
        Assert.NotNull(scene.Sandbox.Canvas.Navigation.EditRequested);
        Focus(harness, draw, "reset-layout");
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Assert.Equal(4, scene.Sandbox.Nodes.Count);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    public void DraggingPreservesGroupSelectionAndUsesWorldCoordinates(double zoom)
    {
        using var harness = new FrameHarness(1000, 1200);
        var scene = new GraphPlayground();
        scene.Sandbox.Canvas.ShowMinimap = false;
        scene.Sandbox.Canvas.Camera.SetView(default, zoom);
        Action<Gui> draw = scene.Draw;
        harness.Frame(draw);
        harness.Frame(draw);
        var first = scene.Sandbox.Nodes[0];
        var second = scene.Sandbox.Nodes[1];
        harness.Click(draw, scene.Sandbox.Canvas.WorldToScreen(first.Bounds.Center));
        Assert.Contains(first.Key, scene.Sandbox.Canvas.Navigation.SelectedKeys);
        harness.Input.PressKey(KeyboardKey.LeftControl);
        harness.Click(draw, scene.Sandbox.Canvas.WorldToScreen(second.Bounds.Center));
        harness.Input.ReleaseKey(KeyboardKey.LeftControl);
        harness.Frame(draw);
        Assert.Equal(2, scene.Sandbox.Canvas.Navigation.SelectedKeys.Count);
        var start = scene.Sandbox.Canvas.WorldToScreen(first.Bounds.Center);
        harness.Input.MoveTo(start);
        harness.Input.PressButton();
        harness.Frame(draw);
        harness.Input.MoveTo(start + new Vector2(60, 30));
        harness.Frame(draw);
        harness.Input.MoveTo(start + new Vector2(80, 40));
        harness.Frame(draw);
        harness.Input.ReleaseButton();
        harness.Frame(draw);
        Assert.Equal(first.Bounds.X + 80 / zoom, scene.Sandbox.Nodes[0].Bounds.X, 5);
        Assert.Equal(second.Bounds.Y + 40 / zoom, scene.Sandbox.Nodes[1].Bounds.Y, 5);
        Assert.Equal(2, scene.Sandbox.Canvas.Navigation.SelectedKeys.Count);
        var moved = scene.Sandbox.Nodes[0].Bounds;
        harness.Input.PressButton(MouseButton.Right);
        harness.Frame(draw);
        harness.Input.MoveTo(start + new Vector2(100, 50));
        harness.Frame(draw);
        harness.Input.ReleaseButton(MouseButton.Right);
        harness.Frame(draw);
        Assert.Equal(moved, scene.Sandbox.Nodes[0].Bounds);
    }

    [Fact]
    public void ViewSelectionAndPendingScenariosUseTheRealSemanticModel()
    {
        using var harness = new FrameHarness(1000, 700);
        var scene = new GraphPlayground();
        Action<Gui> draw = scene.Draw;
        harness.Frame(draw);
        harness.Frame(draw);
        var cards = scene.Sandbox.Nodes;
        scene.Sandbox.SelectNode("missing", false);
        scene.Sandbox.SelectNode(cards[0].Key, false);
        scene.Sandbox.SelectNode(cards[1].Key, true);
        scene.Sandbox.SelectNode(cards[1].Key, true);
        Assert.Single(scene.Sandbox.Canvas.Navigation.SelectedKeys);
        scene.Sandbox.SelectNode(cards[2].Key, false);
        Assert.Equal([cards[2].Key], scene.Sandbox.Canvas.Navigation.SelectedKeys);
        scene.Sandbox.MoveSelection(new GraphPoint(20, 30));
        harness.Frame(draw);
        Assert.Equal(380, cards[2].Bounds.X);
        scene.Sandbox.SelectNode(cards[2].Key, false);
        scene.SelectScenario(1);
        harness.Frame(draw);
        scene.SelectScenario(2);
        harness.Frame(draw);
        scene.SelectScenario(0);
        harness.Frame(draw);
        Assert.All(cards, static card => Assert.Equal(GraphElementRole.Node, card.Role));
        Assert.DoesNotContain(typeof(GraphPlayground).Assembly.GetReferencedAssemblies(), static assembly =>
            assembly.Name!.Contains("Excalibur", StringComparison.Ordinal));
    }

    static void Focus(FrameHarness harness, Action<Gui> draw, string id)
    {
        harness.Gui.Focus.RequestFocus(id);
        harness.Frame(draw);
    }

    sealed class SemanticsSink : IControlSemanticsSink
    {
        public List<ControlSemantics> Controls { get; } = [];
        public void Publish(ControlSemantics semantics) => Controls.Add(semantics);
    }
}

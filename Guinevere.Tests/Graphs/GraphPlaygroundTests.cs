using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Graphs;

public sealed class GraphPlaygroundTests
{
    [Fact]
    public void ScenarioDropdownUsesExcaliburAndSupportsKeyboardNavigation()
    {
        using var harness = new FrameHarness(1000, 700);
        var scene = new GraphPlayground();
        var sink = new SemanticsSink();
        harness.Gui.ControlSemantics = sink;
        harness.Frame(scene.Draw);
        harness.Frame(scene.Draw);
        var picker = sink.Controls.Last(static control => control.Label == "Scenario");
        Assert.Contains("Sandbox", picker.Value, StringComparison.Ordinal);
        Focus(harness, scene.Draw, picker.Id);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Enter);
        harness.Frame(scene.Draw);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.End);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Enter);
        harness.Frame(scene.Draw);
        harness.Frame(scene.Draw);
        Assert.Equal(3, scene.Scenario);
        Assert.NotEmpty(scene.Groups.Sandbox.View.Editor.Document.Groups);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SelectScenario(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SelectScenario(4));
        Assert.Contains(typeof(GraphPlayground).Assembly.GetReferencedAssemblies(), static assembly =>
            assembly.Name == "Guinevere.Excalibur");
        Assert.DoesNotContain(typeof(GraphCanvas).Assembly.GetReferencedAssemblies(), static assembly =>
            assembly.Name!.Contains("Excalibur", StringComparison.Ordinal));
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
        GraphPlaygroundInput.Command(harness, scene, "reset-layout");
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
        Assert.DoesNotContain(typeof(GraphCanvas).Assembly.GetReferencedAssemblies(), static assembly =>
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

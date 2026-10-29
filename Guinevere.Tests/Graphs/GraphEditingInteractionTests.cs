using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class GraphEditingInteractionTests
{
    [Fact]
    public void ReplacingAnOccupiedInputIsAtomicAndAnIdenticalLinkIsANoOp()
    {
        var scene = new SandboxScene();
        var document = scene.Graph.Editor.Document;
        var old = document.Edges.First();
        var other = document.Edges.First(edge => edge.Target != old.Target && edge.Source != old.Source);
        Assert.True(scene.View.Connect(other.Source, old.Target).Succeeded);
        var replacement = Assert.Single(document.GetEdges(old.Target));
        Assert.Equal(other.Source, replacement.Source);
        Assert.False(document.TryGetEdge(old.Id, out _));
        Assert.Equal(3, document.Edges.Count);
        var history = scene.Graph.Editor.NextUndo;
        Assert.True(scene.View.Connect(old.Target, other.Source).Succeeded);
        Assert.Same(history, scene.Graph.Editor.NextUndo);
        Assert.Equal(replacement, Assert.Single(document.GetEdges(old.Target)));
        Assert.Empty(scene.View.Diagnostics);
        Assert.True(scene.View.Undo().Succeeded);
        Assert.Equal(old, Assert.Single(document.GetEdges(old.Target)));
        Assert.False(scene.Graph.Editor.CanUndo);
        Assert.True(scene.View.Redo().Succeeded);
        Assert.Equal(replacement, Assert.Single(document.GetEdges(old.Target)));
        Assert.True(document.TryGetEdge(other.Id, out _));
    }

    [Fact]
    public void ProtectedConnectionsKeepCoreDiagnosticsAndFailedReplacementRollsBack()
    {
        var scene = new SandboxScene();
        var document = scene.Graph.Editor.Document;
        var old = document.Edges.First();
        var other = document.Edges.First(edge => edge.Target != old.Target && edge.Source != old.Source);
        scene.View.AvoidReplacingConnections = true;
        Assert.False(scene.View.Connect(old.Source, old.Target).Succeeded);
        Assert.Contains(scene.View.Diagnostics, static d => d.Id == GraphDiagnosticIds.DuplicateEdge);
        Assert.False(scene.View.Connect(other.Source, old.Target).Succeeded);
        Assert.Contains(scene.View.Diagnostics, static d => d.Id == GraphDiagnosticIds.MultiplicityExceeded);
        Assert.Equal(old, Assert.Single(document.GetEdges(old.Target)));
        Assert.False(scene.Graph.Editor.CanUndo);
        scene.View.AvoidReplacingConnections = false;
        var multiply = document.Nodes.Single(static n => n.TypeId == ArithmeticGraph.Multiply.Id);
        Assert.False(scene.View.Connect(multiply.GetPort(ArithmeticGraph.ValueKey).Id, old.Target).Succeeded);
        Assert.Equal(old, Assert.Single(document.GetEdges(old.Target)));
        Assert.Equal(3, document.Edges.Count);
        Assert.NotEmpty(scene.View.Diagnostics);
        Assert.False(scene.Graph.Editor.CanUndo);
    }

    [Fact]
    public void ReplacementRespectsSingleOutputAndMultipleInputMultiplicity()
    {
        var registry = new GraphTypeRegistry();
        var value = new PortType(StableId.New(), "value");
        registry.PortTypes.Register(value);
        var key = StableId.New();
        var output = new NodeType(StableId.New(), "Single output",
            [new PortDeclaration(key, "Output", PortDirection.Output, value.Id, PortMultiplicity.Single)]);
        var input = new NodeType(StableId.New(), "Multiple input",
            [new PortDeclaration(key, "Input", PortDirection.Input, value.Id, PortMultiplicity.Multiple)]);
        registry.NodeTypes.Register(output);
        registry.NodeTypes.Register(input);
        var view = new GraphDocumentView(new GraphEditor(new GraphDocument(registry)));
        ElementId Add(NodeType type) => view.Editor.Document.GetNode(view.AddNode(type, default)).GetPort(key).Id;
        var a = Add(output);
        var b = Add(output);
        var c = Add(input);
        var d = Add(input);
        view.Connect(a, c);
        view.Connect(b, d);
        var old = Assert.Single(view.Editor.Document.GetEdges(a));
        var retained = Assert.Single(view.Editor.Document.GetEdges(b));
        Assert.True(view.Connect(a, d).Succeeded);
        Assert.Equal(2, view.Editor.Document.GetEdges(d).Count);
        Assert.Empty(view.Editor.Document.GetEdges(c));
        Assert.Contains(retained, view.Editor.Document.Edges);
        view.Undo();
        Assert.Equal(old, Assert.Single(view.Editor.Document.GetEdges(c)));
    }

    [Theory]
    [InlineData(KeyboardKey.Delete)]
    [InlineData(KeyboardKey.Backspace)]
    public void DeletingAnOccupiedPortDisconnectsAllItsEdgesAndCancelsThePreview(KeyboardKey key)
    {
        using var harness = new FrameHarness(1200, 900);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        var document = scene.Graph.Editor.Document;
        var source = document.Edges.First().Source;
        var otherTarget = document.Edges.Skip(1).First().Target;
        scene.View.Connect(source, otherTarget);
        scene.Graph.Editor.ClearHistory();
        var attached = document.GetEdges(source).ToArray();
        Assert.Equal(2, attached.Length);
        void Draw(Gui gui) => scene.View.Draw(gui, "sandbox-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Click(Draw, scene.Canvas.WorldToScreen(scene.View.PortPosition(source)));
        Assert.NotNull(scene.Canvas.Navigation.ConnectionSourceKey);
        GraphCanvasTests.Key(harness, Draw, key);
        Assert.Empty(document.GetEdges(source));
        Assert.Equal(4, document.Nodes.Count);
        Assert.Null(scene.Canvas.Navigation.ConnectionSourceKey);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        Assert.Equal(attached.OrderBy(static e => e.Id), document.GetEdges(source).OrderBy(static e => e.Id));
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Y, KeyboardKey.LeftControl);
        Assert.Empty(document.GetEdges(source));
        Assert.False(scene.View.DisconnectPort(source).Succeeded);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    public void OccupiedInputAcceptsAReplacementDragAndIdenticalReconnect(double zoom)
    {
        using var harness = new FrameHarness(1800, 1200);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        scene.Canvas.Camera.SetView(default, zoom);
        var document = scene.Graph.Editor.Document;
        var old = document.Edges.First();
        var source = document.Edges.Skip(1).First().Source;
        void Draw(Gui gui) => scene.View.Draw(gui, "sandbox-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var start = scene.Canvas.WorldToScreen(scene.View.PortPosition(source));
        var end = scene.Canvas.WorldToScreen(scene.View.PortPosition(old.Target));
        Drag(harness, Draw, start, end);
        Assert.Equal(source, Assert.Single(document.GetEdges(old.Target)).Source);
        var history = scene.Graph.Editor.NextUndo;
        harness.Click(Draw, end);
        harness.Click(Draw, start);
        Assert.Same(history, scene.Graph.Editor.NextUndo);
        Assert.Empty(scene.View.Diagnostics);
        Assert.Null(scene.Canvas.Navigation.ConnectionSourceKey);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    public void GroupDragIsOneHistoryStepInterleavedWithDocumentEditsWithoutModelChanges(double zoom)
    {
        using var harness = new FrameHarness(1200, 1400);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        scene.Canvas.Camera.SetView(default, zoom);
        var document = scene.Graph.Editor.Document;
        var changes = 0;
        document.Changed += (_, _) => changes++;
        void Draw(Gui gui) => scene.View.Draw(gui, "sandbox-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var first = document.Nodes.First().Id;
        var second = document.Nodes.Skip(1).First().Id;
        scene.SelectNode(first.ToString(), false);
        scene.SelectNode(second.ToString(), true);
        harness.Frame(Draw);
        var before = scene.View.Position(first);
        var secondBefore = scene.View.Position(second);
        var start = scene.Canvas.WorldToScreen(scene.Nodes[0].Bounds.Center);
        Drag(harness, Draw, start, start + new Vector2(80, 40));
        Assert.Equal(new GraphPoint(before.X + 80 / zoom, before.Y + 40 / zoom), scene.View.Position(first));
        Assert.Equal(0, changes);
        Assert.Empty(document.Positions);
        Assert.Equal("Move nodes", scene.Graph.Editor.NextUndo!.Description);
        scene.AddNode(new GraphPoint(900, 500));
        Assert.Equal(1, changes);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        Assert.Equal(4, document.Nodes.Count);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        Assert.Equal(before, scene.View.Position(first));
        Assert.Equal(secondBefore, scene.View.Position(second));
        Assert.False(scene.Graph.Editor.CanUndo);
        Assert.Equal(2, changes);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Y, KeyboardKey.LeftControl);
        Assert.Equal(before.X + 80 / zoom, scene.View.Position(first).X);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Y, KeyboardKey.LeftControl);
        Assert.Equal(5, document.Nodes.Count);
        Assert.Equal(3, changes);
        Assert.Empty(document.Positions);
    }

    [Fact]
    public void KeyboardNudgesAndResetAreUndoableAndANewEditInvalidatesRedo()
    {
        using var harness = new FrameHarness(1100, 900);
        var playground = new GraphPlayground();
        var scene = playground.Sandbox;
        Action<Gui> draw = playground.Draw;
        harness.Frame(draw);
        harness.Frame(draw);
        var id = scene.Graph.Editor.Document.Nodes.First().Id;
        var before = scene.View.Position(id);
        scene.SelectNode(id.ToString(), false);
        harness.Gui.Focus.RequestFocus("sandbox-canvas");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Right, KeyboardKey.LeftShift);
        Assert.Equal(before.X + 16, scene.View.Position(id).X);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        Assert.Equal(before, scene.View.Position(id));
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Y, KeyboardKey.LeftControl);
        scene.ResetLayout();
        Assert.Equal(before, scene.View.Position(id));
        scene.View.Undo();
        Assert.Equal(before.X + 16, scene.View.Position(id).X);
        scene.View.MoveSelection(new GraphPoint(0, 16));
        Assert.False(scene.View.Redo().Succeeded);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scene.View.MoveSelection(new GraphPoint(double.NaN, 0)));
    }

    [Fact]
    public void MinimapAndConnectionProtectionTogglesWorkWithKeyboardAndPublishTheirState()
    {
        using var harness = new FrameHarness(1100, 900);
        var playground = new GraphPlayground();
        var sink = Substitute.For<IControlSemanticsSink>();
        harness.Gui.ControlSemantics = sink;
        Action<Gui> draw = playground.Draw;
        harness.Frame(draw);
        harness.Frame(draw);
        Assert.False(playground.ShowPendingScenarios);
        const string resourceName = "GuinevereDemos.guinevere-badge.png";
        using var resource = typeof(GraphPlayground).Assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(resource);
        harness.Gui.Focus.RequestFocus("toggle-minimap");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Space);
        harness.Frame(draw);
        Assert.False(playground.Sandbox.Canvas.ShowMinimap);
        harness.Gui.Focus.RequestFocus("protect-connections");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Assert.True(playground.Sandbox.View.AvoidReplacingConnections);
        sink.Received().Publish(Arg.Is<ControlSemantics>(static s => s.Label == "Avoid replacing connections"
            && s.State.HasFlag(ControlVisualState.Checked)));
        harness.Gui.Focus.RequestFocus("toggle-minimap");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Assert.True(playground.Sandbox.Canvas.ShowMinimap);
        harness.Gui.Focus.RequestFocus("select-style");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Space);
        harness.Frame(draw);
        Assert.Equal(1, playground.StyleIndex);
        Assert.Contains(GraphDemoStyles.Sheets[1], harness.Gui.StyleSheets);
        harness.Gui.Focus.RequestFocus("cycle-routing");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Assert.Equal(GraphLinkRouting.Straight, playground.Flow.Sandbox.View.Routing);
        var edge = playground.Sandbox.Graph.Editor.Document.Edges.First();
        Assert.Equal(GraphLinkRouting.Straight, playground.Sandbox.View.Connector(edge.Id).Routing);
        playground.CycleRouting();
        playground.CycleRouting();
        playground.CycleStyle();
        playground.CycleStyle();
        Assert.Equal(GraphLinkRouting.Bezier, playground.Sandbox.View.Routing);
        Assert.Equal(0, playground.StyleIndex);
    }

    static void Drag(FrameHarness harness, Action<Gui> draw, Vector2 start, Vector2 end)
    {
        harness.Input.MoveTo(start);
        harness.Input.PressButton();
        harness.Frame(draw);
        harness.Input.MoveTo((start + end) / 2);
        harness.Frame(draw);
        harness.Input.MoveTo(end);
        harness.Frame(draw);
        harness.Input.ReleaseButton();
        harness.Frame(draw);
    }
}

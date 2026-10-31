using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class GraphDocumentViewTests
{
    [Fact]
    public void LayoutResetPreservesNodesNamesPortsEdgesPayloadsAndHistory()
    {
        var scene = new SandboxScene();
        var document = scene.Graph.Editor.Document;
        scene.AddNode(new GraphPoint(300, 400));
        var before = document.Nodes.Select(static node => (node.Id, node.Payload,
            Ports: node.Ports.Select(static port => port.Id).ToArray())).ToArray();
        var names = scene.Nodes.Select(static node => node.Name).ToArray();
        var edges = document.Edges.ToArray();
        var history = scene.Graph.Editor.NextUndo;
        scene.SelectNode(scene.Nodes[0].Key, false);
        scene.MoveSelection(new GraphPoint(100, 80));
        scene.ResetLayout();
        Assert.Equal(5, document.Nodes.Count);
        Assert.Equal(names, scene.Nodes.Select(static node => node.Name));
        Assert.Equal(new GraphPoint(40, 40), scene.View.Position(before[0].Id));
        Assert.Equal(new GraphPoint(300, 400), scene.View.Position(before[^1].Id));
        Assert.Equal(edges, document.Edges);
        Assert.Equal("Reset layout", scene.Graph.Editor.NextUndo!.Description);
        Assert.True(scene.View.Undo().Succeeded);
        Assert.Equal(new GraphPoint(140, 120), scene.View.Position(before[0].Id));
        Assert.True(scene.View.Undo().Succeeded);
        Assert.Same(history, scene.Graph.Editor.NextUndo);
        foreach (var item in before)
        {
            var node = document.GetNode(item.Id);
            Assert.Equal(item.Payload, node.Payload);
            Assert.Equal(item.Ports, node.Ports.Select(static port => port.Id));
        }
    }

    [Fact]
    public void NormalClickReplacesSelectionIncludingAnAlreadySelectedNode()
    {
        using var harness = new FrameHarness(1200, 1100);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        void Draw(Gui gui) => scene.View.Draw(gui, "sandbox-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var first = scene.Nodes[0];
        var second = scene.Nodes[1];
        harness.Click(Draw, scene.Canvas.WorldToScreen(first.Bounds.Center));
        harness.Input.PressKey(KeyboardKey.LeftControl);
        harness.Click(Draw, scene.Canvas.WorldToScreen(second.Bounds.Center));
        harness.Input.ReleaseKey(KeyboardKey.LeftControl);
        harness.Frame(Draw);
        Assert.Equal(2, scene.Canvas.Navigation.SelectedKeys.Count);
        harness.Click(Draw, scene.Canvas.WorldToScreen(second.Bounds.Center));
        Assert.Equal([second.Key], scene.Canvas.Navigation.SelectedKeys);
        harness.Click(Draw, scene.Canvas.WorldToScreen(first.Bounds.Center));
        Assert.Equal([first.Key], scene.Canvas.Navigation.SelectedKeys);
        harness.Click(Draw, scene.Canvas.WorldToScreen(new GraphPoint(600, 500)));
        Assert.Empty(scene.Canvas.Navigation.SelectedKeys);
        Assert.False(scene.Canvas.Navigation.Select("missing"));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    public void PortsSupportClickAndDragConnectionsWithRealUndoAndRedo(double zoom)
    {
        using var harness = new FrameHarness(1600, 1100);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        scene.Canvas.Camera.SetView(default, zoom);
        var document = scene.Graph.Editor.Document;
        var edge = document.Edges.First();
        Assert.True(scene.Graph.Editor.Execute(new DisconnectCommand(edge.Id)).Succeeded);
        void Draw(Gui gui) => scene.View.Draw(gui, "sandbox-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var source = scene.Canvas.WorldToScreen(scene.View.PortPosition(edge.Source));
        var target = scene.Canvas.WorldToScreen(scene.View.PortPosition(edge.Target));
        Assert.Contains(scene.View.Elements, static element => element.Role == GraphElementRole.Port);
        harness.Click(Draw, source);
        Assert.Equal(edge.Source.ToString(), scene.Canvas.Navigation.ConnectionSourceKey);
        harness.Click(Draw, target);
        Assert.Equal(3, document.Edges.Count);
        Assert.Null(scene.Canvas.Navigation.ConnectionSourceKey);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Z, KeyboardKey.LeftControl);
        Assert.Equal(2, document.Edges.Count);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Y, KeyboardKey.LeftControl);
        Assert.Equal(3, document.Edges.Count);
        scene.View.Undo();
        harness.Frame(Draw);
        harness.Input.MoveTo(source);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo((source + target) / 2);
        harness.Frame(Draw);
        harness.Input.MoveTo(target);
        harness.Frame(Draw);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);
        Assert.Equal(3, document.Edges.Count);
        Assert.Null(scene.Canvas.Navigation.ConnectionSourceKey);
        var rejected = scene.Canvas.Navigation.EditRequested!(
            new GraphEditRequest(GraphEditKind.Connect, edge.Source.ToString(), edge.Source.ToString()));
        Assert.False(rejected.Succeeded);
        Assert.Contains("MG", rejected.Message);
        var invalid = scene.View.Connect(edge.Source, edge.Source);
        Assert.False(invalid.Succeeded);
        Assert.NotEmpty(scene.View.Diagnostics);
        Assert.Equal(3, document.Edges.Count);
        Assert.True(scene.View.Connect(edge.Target, ElementId.New()).Diagnostics.Count > 0);
    }

    [Fact]
    public void CurveSelectionDeletesAndRestoresTheSameRealEdge()
    {
        using var harness = new FrameHarness(1200, 900);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        void Draw(Gui gui) => scene.View.Draw(gui, "sandbox-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var edge = scene.Graph.Editor.Document.Edges.First();
        var connector = scene.View.Connector(edge.Id);
        harness.Click(Draw, scene.Canvas.WorldToScreen(connector.Point(0.5)));
        Assert.Equal([edge.Id.ToString()], scene.Canvas.Navigation.SelectedKeys);
        GraphCanvasTests.Key(harness, Draw, KeyboardKey.Delete);
        Assert.False(scene.Graph.Editor.Document.TryGetEdge(edge.Id, out _));
        scene.View.Undo();
        Assert.True(scene.Graph.Editor.Document.TryGetEdge(edge.Id, out var restored));
        Assert.Equal(edge, restored);
    }

    [Fact]
    public void PaletteAndEvaluationUsePublishedRegistryAndDataflowContracts()
    {
        var scene = new SandboxScene();
        Assert.Equal([ArithmeticGraph.Multiply], scene.View.Palette("MULT"));
        var registry = scene.Graph.Editor.Document.Registry;
        var extra = new NodeType(StableId.New(), "Domain extension", []);
        registry.NodeTypes.Register(extra);
        Assert.Contains(extra, scene.View.Palette("extension"));
        var sum = scene.View.AddNode(ArithmeticGraph.Add, default);
        var numbers = scene.Graph.Editor.Document.Nodes.Where(static node => node.TypeId == ArithmeticGraph.Number.Id)
            .ToArray();
        var sumNode = scene.Graph.Editor.Document.GetNode(sum);
        scene.View.Connect(numbers[0].GetPort(ArithmeticGraph.ValueKey).Id, sumNode.GetPort(ArithmeticGraph.AKey).Id);
        scene.View.Connect(numbers[1].GetPort(ArithmeticGraph.ValueKey).Id, sumNode.GetPort(ArithmeticGraph.BKey).Id);
        var layout = new InstanceLayoutBuilder();
        var diagnostics = new DiagnosticBag();
        var program = scene.Graph.Semantics.Compile(scene.Graph.Editor.Document, layout, diagnostics);
        Assert.NotNull(program);
        Assert.False(diagnostics.HasErrors);
        var instances = new InstanceBuffer(layout.Build());
        var instance = instances.Allocate();
        program.BeginEvaluation(instances, instance);
        var result = scene.Graph.Editor.Document.Nodes.Single(static node => node.TypeId == ArithmeticGraph.Result.Id);
        Assert.Equal(6f, program.Read<float>(new EvaluationContext(), instances, instance,
            result.GetPort(ArithmeticGraph.ValueKey).Id));
        Assert.Equal(5f, program.Read<float>(new EvaluationContext(), instances, instance,
            sumNode.GetPort(ArithmeticGraph.ValueKey).Id));
    }

    [Fact]
    public void ConnectorGeometryIncludesBackwardsLinksAndSupportsHitTesting()
    {
        var curve = new GraphConnector(new GraphPoint(500, -100), new GraphPoint(0, 100));
        Assert.Equal(curve.Source, curve.Point(-1));
        Assert.Equal(curve.Target, curve.Point(2));
        for (var i = 0; i <= 32; i++)
        {
            var point = curve.Point(i / 32d);
            Assert.True(curve.Bounds.Contains(new GraphBounds(point.X, point.Y, 0, 0)));
        }
        Assert.Equal(0, curve.Distance(curve.Point(0.5)), 6);
        Assert.True(curve.Distance(new GraphPoint(0, 1000)) > 100);
        var same = new GraphConnector(default, default);
        Assert.Equal(0, same.Distance(default));
    }

    [Fact]
    public void InvalidRequestsCannotDeleteNodesAndOpaquePayloadsAreNotInspected()
    {
        using var harness = new FrameHarness(600, 400);
        var registry = new GraphTypeRegistry();
        var type = new NodeType(StableId.New(), "Opaque domain node", []);
        registry.NodeTypes.Register(type);
        var editor = new GraphEditor(new GraphDocument(registry));
        var view = new GraphDocumentView(editor);
        var payload = new OpaquePayload();
        var id = view.AddNode(type, new GraphPoint(20, 30), payload);
        void Draw(Gui gui) => view.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Same(payload, editor.Document.GetNode(id).Payload);
        Assert.Equal("Opaque domain node", view.Nodes[0].Name);
        Assert.Throws<ArgumentOutOfRangeException>(() => view.AddNode(type, new GraphPoint(double.NaN, 0)));
        var edit = view.Canvas.Navigation.EditRequested!;
        Assert.False(edit(new GraphEditRequest(GraphEditKind.Connect, id.ToString(), "invalid")).Succeeded);
        Assert.False(edit(new GraphEditRequest(GraphEditKind.Delete, "invalid")).Succeeded);
        Assert.False(edit(new GraphEditRequest(GraphEditKind.Delete, ElementId.New().ToString())).Succeeded);
        Assert.Single(editor.Document.Nodes);
        Assert.True(view.AddNode(new NodeType(StableId.New(), "Unregistered", []), default).IsEmpty);
        Assert.NotEmpty(view.Diagnostics);
        Assert.True(view.Undo().Succeeded);
        view.Refresh();
        Assert.Empty(view.Nodes);
        Assert.False(view.Undo().Succeeded);
        Assert.True(view.Redo().Succeeded);
        Assert.False(view.Redo().Succeeded);
    }

    [Fact]
    public void RegistrySearchAndToolbarHistoryCreateAndUndoRealNodes()
    {
        using var harness = new FrameHarness(1200, 1000);
        var semantics = Substitute.For<IControlSemanticsSink>();
        harness.Gui.ControlSemantics = semantics;
        var playground = new GraphPlayground();
        Action<Gui> draw = playground.Draw;
        harness.Frame(draw);
        harness.Frame(draw);
        harness.Gui.Focus.RequestFocus("sandbox-canvas");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.K, KeyboardKey.LeftControl);
        var search = Find(harness.Gui.RootNode!, "node-search")!;
        harness.Click(draw, search.Rect.Center);
        Assert.Equal("node-search", harness.Gui.Focus.CurrentFocusedId);
        harness.Input.TypeText("multiply");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Backspace);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        var document = playground.Sandbox.Graph.Editor.Document;
        Assert.Equal(5, document.Nodes.Count);
        Assert.Equal(ArithmeticGraph.Multiply.Id, document.Nodes.Last().TypeId);
        harness.Gui.Focus.RequestFocus("undo");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Assert.Equal(4, document.Nodes.Count);
        harness.Gui.Focus.RequestFocus("redo");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
        Assert.Equal(5, document.Nodes.Count);
        harness.Gui.Focus.RequestFocus("sandbox-canvas");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.K, KeyboardKey.LeftControl);
        harness.Gui.Focus.RequestFocus("node-search");
        harness.Frame(draw);
        harness.Input.TypeText("no-match");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        Assert.Equal(5, document.Nodes.Count);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Escape);
        semantics.Received().Publish(Arg.Is<ControlSemantics>(static s => s.Label == "Search node types"));
        var port = document.Nodes.First().Ports[0].Id;
        Assert.False(playground.Sandbox.View.Connect(port, port).Succeeded);
        harness.Frame(draw);
    }

    [Fact]
    public void UnknownNodesAndSavedPositionsRemainUntouchedByTheView()
    {
        var registry = new GraphTypeRegistry();
        var type = new NodeType(StableId.New(), "Unavailable domain", []);
        registry.NodeTypes.Register(type);
        var editor = new GraphEditor(new GraphDocument(registry));
        var command = new AddNodeCommand(type);
        Assert.True(editor.Execute(command).Succeeded);
        Assert.True(editor.Execute(new MoveNodeCommand(command.NodeId, new MASS4.Graph.GraphPoint(17, 23))).Succeeded);
        var json = GraphJson.Serialize(editor.Document);
        var loaded = GraphJson.Deserialize(json, new GraphTypeRegistry());
        Assert.NotNull(loaded.Document);
        var view = new GraphDocumentView(new GraphEditor(loaded.Document))
        {
            NodeTitle = static _ => throw new InvalidOperationException("Unknown nodes reached a domain adapter.")
        };
        view.Refresh();
        Assert.Equal("Unknown node", Assert.Single(view.Nodes).Name);
        Assert.NotNull(view.Nodes[0].Error);
        Assert.Equal(new GraphPoint(17, 23), view.Position(command.NodeId));
        Assert.Equal(json, GraphJson.Serialize(loaded.Document));
    }

    static LayoutNode? Find(LayoutNode root, string id)
    {
        if (root.Id == id)
        {
            return root;
        }
        foreach (var child in root.ChildNodes)
        {
            if (Find(child, id) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    sealed class OpaquePayload
    {
        public override string ToString() => throw new InvalidOperationException("GUI inspected a payload.");
    }
}

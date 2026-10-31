using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class GraphEditorAccessibilityTests
{
    [Fact]
    public void DocumentMetadataIncludesNodeTypesPortStateConnectionsAndDiagnostics()
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        var sink = new Bridge();
        harness.Gui.Platform.Register<IAccessibilityCapability>(sink);
        var document = scene.View.Editor.Document;
        var node = document.Nodes.First();
        var port = node.Ports[0];
        var edge = document.Edges.First(e => e.Source == port.Id);
        scene.View.NodeState = static _ => "Running instance 12";
        scene.View.Validation = [new Diagnostic("MG9001", (MASS4.Graph.DiagnosticSeverity)DiagnosticSeverity.Warning, port.Id, "Check this port"),
            new Diagnostic("MG9002", (MASS4.Graph.DiagnosticSeverity)DiagnosticSeverity.Error, edge.Id, "Check this edge")];
        void Draw(Gui gui) => scene.View.Draw(gui, "accessible-graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var semantic = sink.Elements.Last(s => s.View.Key == node.Id.ToString());
        Assert.Equal("Number", semantic.View.TypeName);
        Assert.Equal("Running instance 12", semantic.View.StateDescription);
        Assert.Contains("MG9001", semantic.View.Error!, StringComparison.Ordinal);
        Assert.Contains("MG9002", semantic.View.Error!, StringComparison.Ordinal);
        var portMetadata = sink.Elements.Last(s => s.View.Key == port.Id.ToString());
        Assert.Contains("Output", portMetadata.View.StateDescription!, StringComparison.Ordinal);
        Assert.Contains("1 connected", portMetadata.View.StateDescription!, StringComparison.Ordinal);
        Assert.Contains(GraphActionKind.Connect, portMetadata.Actions!);
        var link = sink.Elements.Last(s => s.View.Key == edge.Id.ToString());
        Assert.Contains("Number", link.View.Name, StringComparison.Ordinal);
        Assert.Contains("Multiply", link.View.Name, StringComparison.Ordinal);
        Assert.EndsWith(port.Id.ToString(), link.SourceId, StringComparison.Ordinal);
        Assert.Equal("accessible-graph/graph/" + edge.Target, link.TargetId);
        Assert.Contains("Running instance 12", sink.Controls.Last(c => c.Id == semantic.Id).Value!, StringComparison.Ordinal);
        Assert.Contains(sink.Events, static e => e.Kind == GraphSemanticEventKind.ValidationFailed && e.GraphId == "accessible-graph");
        sink.Events.Clear();
        scene.View.Validation = scene.View.Validation.ToArray();
        harness.Frame(Draw);
        Assert.Empty(sink.Events);
        scene.View.Validation = [];
        harness.Frame(Draw);
        Assert.Contains(sink.Events, static e => e.Kind == GraphSemanticEventKind.ValidationChanged);
        Assert.Null(sink.Elements.Last(s => s.View.Key == edge.Id.ToString()).View.Error);
    }

    [Fact]
    public void DirectKeyboardAndDomainCommandsAnnounceWithoutDuplicatingNavigationResults()
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        var sink = new Bridge();
        harness.Gui.ControlSemantics = sink;
        void Draw(Gui gui) => scene.View.Draw(gui, "accessible-graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        sink.Events.Clear();
        var node = scene.View.Editor.Document.Nodes.First();
        scene.View.Canvas.Navigation.Select(node.Id.ToString());
        scene.View.MoveSelection(new GraphPoint(16, 0));
        scene.View.Execute(new SetPayloadCommand(node.Id, 7f));
        scene.View.Undo();
        scene.View.Redo();
        harness.Frame(Draw);
        Assert.Equal(4, sink.Events.Count(static e => e.Kind == GraphSemanticEventKind.CommandCompleted));
        sink.Events.Clear();
        var invalid = scene.View.Editor.Document.Nodes.First(static n => n.TypeId == ArithmeticGraph.Multiply.Id).Ports[0].Id;
        scene.Canvas.Navigation.Connect(invalid.ToString(), invalid.ToString());
        harness.Frame(Draw);
        Assert.Single(sink.Events, static e => e.Kind == GraphSemanticEventKind.ValidationFailed);
        sink.Events.Clear();
        var edge = scene.View.Editor.Document.Edges.First();
        scene.View.Connect(edge.Source, edge.Target);
        harness.Frame(Draw);
        Assert.Empty(sink.Events);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(1)]
    [InlineData(2)]
    public void PlatformActionsRevealVirtualizedNodesAndEditWithTheSameHistory(double zoom)
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        var bridge = new Bridge();
        harness.Gui.ControlSemantics = bridge;
        harness.Gui.Platform.Register<IGraphActionSource>(bridge);
        var view = scene.View;
        view.Canvas.Camera.SetView(new GraphPoint(50_000, 50_000), zoom);
        void Draw(Gui gui) => view.Draw(gui, "accessible-graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var key = scene.View.Editor.Document.Nodes.First().Id.ToString();
        Assert.True(bridge.Elements.Last(s => s.View.Key == key).Virtualized);
        Act(harness, Draw, bridge, new(GraphActionKind.Focus, key));
        Assert.Equal(key, view.Canvas.Navigation.FocusedKey);
        Assert.False(bridge.Elements.Last(s => s.View.Key == key).Virtualized);
        Act(harness, Draw, bridge, new(GraphActionKind.Select, key));
        Act(harness, Draw, bridge, new(GraphActionKind.ToggleSelection, key));
        Assert.Empty(view.Canvas.Navigation.SelectedKeys);
        Act(harness, Draw, bridge, new(GraphActionKind.ToggleSelection, key));
        Act(harness, Draw, bridge, new(GraphActionKind.Activate, key));
        Act(harness, Draw, bridge, new(GraphActionKind.Activate));
        Act(harness, Draw, bridge, new(GraphActionKind.Cancel));
        Act(harness, Draw, bridge, new(GraphActionKind.Copy));
        Act(harness, Draw, bridge, new(GraphActionKind.Paste));
        Assert.Equal(5, view.Editor.Document.Nodes.Count);
        Act(harness, Draw, bridge, new(GraphActionKind.Delete));
        Assert.Equal(4, view.Editor.Document.Nodes.Count);
        Act(harness, Draw, bridge, new(GraphActionKind.Undo));
        Assert.Equal(5, view.Editor.Document.Nodes.Count);
        Act(harness, Draw, bridge, new(GraphActionKind.Redo));
        Assert.Equal(4, view.Editor.Document.Nodes.Count);
        Assert.Contains(bridge.Elements.Last(static s => s.View.Role == GraphElementRole.Graph).Actions!,
            static a => a == GraphActionKind.Undo);
    }

    [Fact]
    public void ActionsAreScopedAndCanConnectInspectSearchAndDeletePorts()
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        var bridge = new Bridge();
        harness.Gui.Platform.Register<IAccessibilityCapability>(bridge);
        void Draw(Gui gui) => scene.View.Draw(gui, "accessible-graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        bridge.Enqueue("another-graph", new(GraphActionKind.SelectAll));
        harness.Frame(Draw);
        Assert.Single(bridge.Pending);
        var document = scene.View.Editor.Document;
        var edge = document.Edges.First();
        bridge.Events.Clear();
        Act(harness, Draw, bridge, new(GraphActionKind.Connect, edge.Source.ToString(), edge.Target.ToString()));
        Assert.DoesNotContain(bridge.Events, static e => e.Kind == GraphSemanticEventKind.ConnectionCreated
            || e.Kind == GraphSemanticEventKind.ValidationFailed);
        Act(harness, Draw, bridge, new(GraphActionKind.Delete, edge.Source.ToString()));
        Assert.Empty(document.GetEdges(edge.Source));
        Act(harness, Draw, bridge, new(GraphActionKind.Undo));
        Assert.NotEmpty(document.GetEdges(edge.Source));
        var search = new List<GraphSearchKind>();
        scene.Canvas.Navigation.SearchRequested = search.Add;
        Act(harness, Draw, bridge, new(GraphActionKind.SearchCommands));
        Act(harness, Draw, bridge, new(GraphActionKind.CreateNode));
        Assert.Equal([GraphSearchKind.Commands, GraphSearchKind.CreateNode], search);
        Act(harness, Draw, bridge, new(GraphActionKind.SelectAll));
        Assert.NotEmpty(scene.Canvas.Navigation.SelectedKeys);
        Act(harness, Draw, bridge, new(GraphActionKind.Cut));
        Assert.Empty(document.Nodes);
        Act(harness, Draw, bridge, new(GraphActionKind.Undo));
        Assert.Equal(4, document.Nodes.Count);
        Act(harness, Draw, bridge, new(GraphActionKind.Focus, "missing"));
        Assert.Contains(bridge.Events, static e => e.Message.Contains("unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public void GenericCanvasActionsValidateEndpointsAndDelegateWithoutAnEditorView()
    {
        using var harness = new FrameHarness();
        var bridge = new Bridge();
        harness.Gui.ControlSemantics = bridge;
        var canvas = new GraphCanvas();
        var edits = new List<GraphEditRequest>();
        canvas.Navigation.EditRequested = request =>
        {
            edits.Add(request);
            return new GraphEditResult(true, "Completed");
        };
        void Draw(Gui gui) => canvas.Draw(gui, GraphNavigationTests.Catalog(), id: "accessible-graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Empty(bridge.Elements.Last(static e => e.View.Key == "disabled").Actions!);
        Act(harness, Draw, bridge, new(GraphActionKind.Select));
        Act(harness, Draw, bridge, new(GraphActionKind.Activate, "out"));
        Assert.Equal("out", canvas.Navigation.ConnectionSourceKey);
        Act(harness, Draw, bridge, new(GraphActionKind.Connect, "out", "in"));
        Assert.Null(canvas.Navigation.ConnectionSourceKey);
        Assert.Equal(GraphEditKind.Connect, edits[^1].Kind);
        Assert.Equal(GraphSemanticEventKind.ConnectionCreated, bridge.Events[^1].Kind);
        Act(harness, Draw, bridge, new(GraphActionKind.Delete, "edge"));
        Assert.Equal(GraphEditKind.Delete, edits[^1].Kind);
        Act(harness, Draw, bridge, new(GraphActionKind.Cancel, "in"));
        Assert.Equal("b", canvas.Navigation.FocusedKey);
        Act(harness, Draw, bridge, new(GraphActionKind.Undo));
        Assert.Contains(bridge.Events, static e => e.Message.Contains("No editor", StringComparison.Ordinal));
        foreach (var action in new GraphAction[] { new(GraphActionKind.Connect),
            new(GraphActionKind.Connect, "out"), new(GraphActionKind.Connect, "a", "in"),
            new(GraphActionKind.Connect, "out", "missing"), new(GraphActionKind.Connect, "out", "disabled") })
        {
            Act(harness, Draw, bridge, action);
            Assert.Equal("Two available ports are required.", bridge.Events[^1].Message);
        }
    }

    [Fact]
    public void UnavailableNodesSkipStateAndRoutingCallbacksAndRemainAccessible()
    {
        var scene = new FsmScene { AutoRun = false };
        var loaded = GraphDocumentView.Load(scene.View.Serialize(), new GraphTypeRegistry()).View!;
        loaded.NodeState = static _ => throw new InvalidOperationException();
        loaded.RouteLink = static (_, _) => throw new InvalidOperationException();
        using var harness = new FrameHarness(1100, 700);
        var sink = new Bridge();
        harness.Gui.ControlSemantics = sink;
        void Draw(Gui gui) => loaded.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.All(sink.Elements.Where(static s => s.View.Role == GraphElementRole.Node), static node =>
        {
            Assert.Equal("Unknown node", node.View.Name);
            Assert.Contains("preserved", node.View.StateDescription!, StringComparison.Ordinal);
            Assert.NotNull(node.View.Error);
        });
    }

    static void Act(FrameHarness harness, Action<Gui> draw, Bridge bridge, GraphAction action)
    {
        bridge.Enqueue("accessible-graph", action);
        harness.Frame(draw);
        harness.Frame(draw);
        harness.Frame(draw);
    }

    sealed class Bridge : IAccessibilityCapability, IGraphSemanticsSink, IGraphActionSource
    {
        public List<ControlSemantics> Controls { get; } = [];
        public List<GraphElementSemantics> Elements { get; } = [];
        public List<GraphSemanticEvent> Events { get; } = [];
        public List<(string Graph, GraphAction Action)> Pending { get; } = [];
        public void BeginFrame() { }
        public void Publish(ControlSemantics semantics) => Controls.Add(semantics);
        public void PublishGraph(GraphElementSemantics semantics) => Elements.Add(semantics);
        public void Announce(GraphSemanticEvent change) => Events.Add(change);
        public void Enqueue(string graph, GraphAction action) => Pending.Add((graph, action));
        public bool TryDequeue(string graphId, out GraphAction action)
        {
            var index = Pending.FindIndex(p => p.Graph == graphId);
            if (index < 0)
            {
                action = null!;
                return false;
            }
            action = Pending[index].Action;
            Pending.RemoveAt(index);
            return true;
        }

        public void EndFrame() { }
    }
}

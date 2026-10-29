using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using MASS4.Graph.State;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class FsmSceneTests
{
    [Fact]
    public async Task ThousandInstancesShareAnAssetAndOnlyTheAddressedInstanceReceivesTheSignal()
    {
        var scene = new FsmScene { AutoRun = false };
        var runtime = scene.Simulation;
        runtime.Compile(scene.Domain);
        Assert.Empty(runtime.Diagnostics);
        Assert.True(runtime.TryAdvance(0));
        await runtime.Completion;
        Assert.Equal(1000, runtime.Snapshot.States.Count);
        Assert.Equal(2, runtime.Snapshot.States.Distinct().Count());
        var patrol = runtime.Snapshot.States[0];
        var chase = runtime.Snapshot.States[1];
        Assert.NotEqual(patrol, chase);
        Assert.True(runtime.TryAdvance(0, FsmDomain.LegacySeePlayer));
        await runtime.Completion;
        Assert.Equal(chase, runtime.Snapshot.States[0]);
        Assert.Equal(patrol, runtime.Snapshot.States[2]);
        Assert.True(runtime.TryAdvance(0, FsmDomain.Hit.Id));
        await runtime.Completion;
        Assert.Equal(chase, runtime.Snapshot.States[0]);
        Assert.True(runtime.TryAdvance(0, FsmDomain.LosePlayer.Id));
        await runtime.Completion;
        Assert.Equal(patrol, runtime.Snapshot.States[0]);
        Assert.Null(runtime.Error);
        Assert.Equal(4, runtime.Snapshot.Tick);
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.TryAdvance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.TryAdvance(1000));
    }

    [Fact]
    public void TriggerRenamePersistenceAndUndoUseRegisteredIdentities()
    {
        var scene = new FsmScene { AutoRun = false };
        var document = scene.Domain.Editor.Document;
        var transition = document.Nodes.Single(node => node.Annotations.TryGetValue(StateNodes.Trigger.Id, out var value)
            && value is StableId id && id == FsmDomain.LegacySeePlayer);
        var before = scene.View.Serialize();
        foreach (var key in transition.Ports.Select(static port => port.Id)
            .Concat(document.Edges.Where(edge => transition.Ports.Any(port => port.Id == edge.Source || port.Id == edge.Target))
                .Select(static edge => edge.Id)))
        {
            scene.View.Canvas.Navigation.Focus(key.ToString());
            scene.SelectSignal(FsmDomain.Hit);
            Assert.True(scene.AssignTrigger().Succeeded);
            Assert.Equal(FsmDomain.Hit.Id, transition.Annotations[StateNodes.Trigger.Id]);
            scene.View.Undo();
        }
        scene.View.Canvas.Navigation.Focus(transition.Id.ToString());
        scene.SelectSignal(FsmDomain.Hit);
        Assert.True(scene.AssignTrigger().Succeeded);
        Assert.Equal(FsmDomain.Hit.Id, document.GetNode(transition.Id).Annotations[StateNodes.Trigger.Id]);
        scene.View.Undo();
        Assert.Equal(before, scene.View.Serialize());
        scene.View.Redo();
        scene.View.Undo();
        var loaded = scene.Load(before, renamed: true);
        Assert.NotNull(loaded.View);
        Assert.Equal(FsmDomain.TargetSpotted.Id,
            scene.Domain.Editor.Document.GetNode(transition.Id).Annotations[StateNodes.Trigger.Id]);
        Assert.DoesNotContain(FsmDomain.LegacySeePlayer.ToString(), scene.View.Serialize(), StringComparison.Ordinal);
        Assert.Contains("Target spotted", scene.Domain.Title(scene.Domain.Editor.Document.GetNode(transition.Id)), StringComparison.Ordinal);
        scene.SelectSignal(new SignalType(FsmDomain.LegacySeePlayer, "legacy declaration"));
        Assert.Equal(FsmDomain.TargetSpotted.Id, scene.SelectedSignal.Id);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SelectSignal(new SignalType(StableId.New(), "unregistered")));
        var editor = scene.View.Editor;
        Assert.Null(scene.Load("malformed").View);
        Assert.Same(editor, scene.View.Editor);
        Assert.False(editor.CanUndo);
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SelectInstance(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => scene.SelectInstance(1000));
    }

    [Fact]
    public async Task CompileDiagnosticsAndEvaluationFailuresReachTheEditor()
    {
        using var harness = new FrameHarness(1280, 1000);
        var scene = new FsmScene { AutoRun = false };
        var entry = scene.Domain.Editor.Document.Nodes.Single(static n => n.TypeId == StateNodes.Entry.Id);
        scene.View.Execute(new RemoveNodeCommand(entry.Id));
        void Draw(Gui gui) => scene.Draw(gui, static (_, _, _) => { });
        harness.Frame(Draw);
        Assert.Contains(scene.View.Validation, static d => d.Id == StateDiagnosticIds.EntryCount);
        Assert.False(scene.Simulation.TryAdvance(0));
        scene.View.Undo();
        harness.Frame(Draw);
        await scene.Simulation.Completion;
        Assert.Empty(scene.View.Validation);
        scene.Simulation.Compile(scene.Domain);
        var exploding = new NodeType(StableId.New(), "Exploding", [StateNodes.Enter(), StateNodes.Exit()]);
        scene.Domain.Registry.NodeTypes.Register(exploding);
        scene.Domain.States.AddState(exploding.Id, new ThrowingState());
        var node = scene.View.AddNode(exploding, default);
        scene.View.Connect(scene.Domain.Editor.Document.GetNode(entry.Id).GetPort(StateNodes.StartKey).Id,
            scene.Domain.Editor.Document.GetNode(node).GetPort(StateNodes.EnterKey).Id);
        harness.Frame(Draw);
        await scene.Simulation.Completion;
        harness.Frame(Draw);
        Assert.IsType<InvalidOperationException>(scene.Simulation.Error);
    }

    [Fact]
    public async Task BackgroundTicksNeverOverlapAndRecompileDiscardsAnOldPublication()
    {
        var scene = new FsmScene { AutoRun = false };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var gated = new NodeType(StableId.New(), "Gated", [StateNodes.Enter(), StateNodes.Exit()]);
        scene.Domain.Registry.NodeTypes.Register(gated);
        scene.Domain.States.AddState(gated.Id, new GatedState(entered, release));
        var node = scene.View.AddNode(gated, default);
        var entry = scene.Domain.Editor.Document.Nodes.Single(static n => n.TypeId == StateNodes.Entry.Id);
        scene.View.Connect(entry.GetPort(StateNodes.StartKey).Id,
            scene.Domain.Editor.Document.GetNode(node).GetPort(StateNodes.EnterKey).Id);
        scene.Simulation.Compile(scene.Domain);
        Assert.True(scene.Simulation.TryAdvance(0));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.True(scene.Simulation.Running);
            Assert.False(scene.Simulation.TryAdvance(0));
            var replacement = new FsmScene { AutoRun = false };
            scene.Simulation.Compile(replacement.Domain);
        }
        finally
        {
            release.Set();
        }
        await scene.Simulation.Completion;
        Assert.Empty(scene.Simulation.Snapshot.States);
        Assert.False(scene.Simulation.Running);
        Assert.True(scene.Simulation.TryAdvance(0));
        await scene.Simulation.Completion;
        Assert.Equal(1000, scene.Simulation.Snapshot.States.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FsmScenarioRendersBothLoopsAndCanCaptureEachPssStyle(int style)
    {
        using var harness = new FrameHarness(1440, 1100);
        var semantics = Substitute.For<IGraphSemanticsSink>();
        harness.Gui.ControlSemantics = semantics;
        var playground = new GraphPlayground();
        playground.Fsm.AutoRun = false;
        playground.SelectScenario(2);
        playground.SelectStyle(style);
        harness.Frame(playground.Draw);
        await playground.Fsm.Simulation.Completion;
        harness.Frame(playground.Draw);
        harness.Frame(playground.Draw);
        playground.Fsm.SelectInstance(1);
        harness.Frame(playground.Draw);
        semantics.Received().Announce(Arg.Is<GraphSemanticEvent>(change =>
            change.Kind == GraphSemanticEventKind.StateChanged && change.GraphId == "fsm-canvas"
            && change.Message == "Instance 1: Chase."));
        playground.Fsm.SelectInstance(0);
        harness.Frame(playground.Draw);
        var view = playground.Fsm.View;
        var inspector = Find(harness.Gui.RootNode!, "fsm-inspector")!;
        Assert.True(inspector.Rect.H < 150, $"Inspector height: {inspector.Rect.H}");
        Assert.All(view.Nodes, node => Assert.True(view.Canvas.VisibleBounds.Contains(node.Bounds)));
        Assert.Equal(6, view.Nodes.Count);
        Assert.Equal(7, view.Editor.Document.Edges.Count);
        Assert.NotEqual(default, playground.Fsm.ActiveState);
        Assert.Contains(view.Nodes, static n => n.StateDescription!.Contains("Selected instance", StringComparison.Ordinal));
        var routes = view.Editor.Document.Edges.Select(edge => view.Connector(edge.Id)).ToArray();
        Assert.Contains(routes, static connector => connector.Polyline().Length > 4);
        foreach (var connector in routes)
        {
            Assert.True(view.Canvas.VisibleBounds.Contains(connector.Bounds));
            foreach (var point in connector.Polyline())
            {
                Assert.True(connector.Bounds.Contains(new GraphBounds(point.X, point.Y, 0, 0)));
            }
        }
        if (Environment.GetEnvironmentVariable("MASS4_GRAPH_CAPTURE_DIR") is { } directory)
        {
            Directory.CreateDirectory(directory);
            using var image = harness.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(directory, $"fsm-{style}.png"));
            png.SaveTo(file);
        }
    }

    [Fact]
    public void ExplicitRoutesAreImmutableAndUseTheSameGeometryForBoundsAndHitTesting()
    {
        GraphPoint[] waypoints = [new(120, 0), new(120, 150), new(-20, 150), new(-20, 50)];
        var route = new GraphConnector(default, new GraphPoint(20, 50), waypoints);
        waypoints[0] = new GraphPoint(9000, 9000);
        var sampled = route.Polyline();
        Assert.Equal(new GraphPoint(120, 0), sampled[1]);
        sampled[1] = default;
        Assert.Equal(new GraphPoint(120, 0), route.Polyline()[1]);
        Assert.Equal(0, route.Distance(new GraphPoint(120, 70)));
        Assert.True(route.Bounds.Contains(new GraphBounds(120, 70, 0, 0)));
        Assert.Equal(default, route.Point(0));
        Assert.Equal(new GraphPoint(20, 50), route.Point(1));
        Assert.Throws<ArgumentOutOfRangeException>(static () => new GraphConnector(default, default,
            [new GraphPoint(double.NaN, 0)]));
    }

    [Fact]
    public async Task KeyboardToolbarAndAccessibleSearchOperateTheRealFsmEditor()
    {
        using var harness = new FrameHarness(1440, 1100);
        var playground = new GraphPlayground();
        var scene = playground.Fsm;
        scene.AutoRun = false;
        playground.SelectScenario(2);
        Action<Gui> draw = playground.Draw;
        harness.Frame(draw);
        await scene.Simulation.Completion;
        harness.Frame(draw);
        Activate(harness, draw, "fsm-next");
        Assert.Equal(1, scene.SelectedInstance);
        Activate(harness, draw, "fsm-previous");
        Assert.Equal(0, scene.SelectedInstance);
        Activate(harness, draw, "fsm-signal");
        var transition = scene.Domain.Editor.Document.Nodes.First(static node => node.TypeId == StateNodes.Transition.Id);
        scene.View.Canvas.Navigation.Focus(transition.Id.ToString());
        Activate(harness, draw, "fsm-assign");
        await scene.Simulation.Completion;
        Assert.Equal(scene.SelectedSignal.Id, transition.Annotations[StateNodes.Trigger.Id]);
        Activate(harness, draw, "fsm-clear-trigger");
        await scene.Simulation.Completion;
        Assert.False(transition.Annotations.ContainsKey(StateNodes.Trigger.Id));
        Activate(harness, draw, "fsm-undo");
        Activate(harness, draw, "fsm-redo");
        await scene.Simulation.Completion;
        Activate(harness, draw, "fsm-send");
        await scene.Simulation.Completion;
        Activate(harness, draw, "fsm-step");
        await scene.Simulation.Completion;
        Activate(harness, draw, "fsm-run");
        Activate(harness, draw, "fsm-run");
        await scene.Simulation.Completion;
        Assert.False(scene.AutoRun);
        Activate(harness, draw, "fsm-save");
        Assert.Contains("mass4.graph", harness.Input.GetClipboardText(), StringComparison.Ordinal);
        Activate(harness, draw, "fsm-load");
        await scene.Simulation.Completion;
        Activate(harness, draw, "fsm-rename");
        await scene.Simulation.Completion;
        Assert.Equal(FsmDomain.TargetSpotted.Id, scene.Domain.Registry.Signals.Resolve(FsmDomain.LegacySeePlayer).Id);
        Activate(harness, draw, "fsm-add-state");
        Activate(harness, draw, "fsm-add-transition");
        await scene.Simulation.Completion;
        Assert.Equal(8, scene.View.Editor.Document.Nodes.Count);
        Activate(harness, draw, "fsm-frame-all");
        var source = new ActionQueue();
        harness.Gui.Platform.Register<IGraphActionSource>(source);
        source.Pending.Enqueue(new GraphAction(GraphActionKind.CreateNode));
        harness.Frame(draw);
        harness.Frame(draw);
        harness.Frame(draw);
        harness.Gui.Focus.RequestFocus("fsm-node-search");
        harness.Frame(draw);
        harness.Input.TypeText("Named");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Backspace);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        await scene.Simulation.Completion;
        Assert.Equal(9, scene.View.Editor.Document.Nodes.Count);
        source.Pending.Enqueue(new GraphAction(GraphActionKind.SearchCommands));
        harness.Frame(draw);
        harness.Frame(draw);
        harness.Gui.Focus.RequestFocus("fsm-node-search");
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Escape);
        scene.View.ContextRequested!(default);
        harness.Frame(draw);
        harness.Click(draw, FrameHarness.Center(Find(harness.Gui.RootNode!, "fsm-node-search")!));
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Right);
        Activate(harness, draw, "fsm-create-" + FsmDomain.NamedState.Id);
        await scene.Simulation.Completion;
        Assert.Equal(10, scene.View.Editor.Document.Nodes.Count);
        harness.Input.SetClipboardText("bad JSON");
        Activate(harness, draw, "fsm-load");
        Assert.Equal(10, scene.View.Editor.Document.Nodes.Count);
        harness.Gui.Platform.Remove<IClipboard>();
        Activate(harness, draw, "fsm-save");
        harness.Frame(draw);
        await scene.Simulation.Completion;
    }

    static void Activate(FrameHarness harness, Action<Gui> draw, string id)
    {
        harness.Gui.Focus.RequestFocus(id);
        harness.Frame(draw);
        GraphCanvasTests.Key(harness, draw, KeyboardKey.Enter);
        harness.Frame(draw);
    }

    static LayoutNode? Find(LayoutNode node, string id) => node.Id == id ? node
        : node.ChildNodes.Select(child => Find(child, id)).FirstOrDefault(static child => child is not null);


    sealed class ActionQueue : IGraphActionSource
    {
        public Queue<GraphAction> Pending { get; } = new();
        public bool TryDequeue(string graphId, out GraphAction action)
        {
            action = null!;
            return graphId == "fsm-canvas" && Pending.TryDequeue(out action!);
        }
    }

    sealed class ThrowingState : IStateNode
    {
        public void OnUpdate(StateContext context) => throw new InvalidOperationException("State evaluation failed.");
    }

    sealed class GatedState(TaskCompletionSource entered, ManualResetEventSlim release) : IStateNode
    {
        public void OnUpdate(StateContext context)
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException();
            }
        }
    }
}

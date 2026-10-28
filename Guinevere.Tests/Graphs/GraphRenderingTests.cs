using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;
using GraphSeverity = MASS4.Graph.DiagnosticSeverity;

namespace Guinevere.Tests.Graphs;

public sealed class GraphRenderingTests
{
    [Theory]
    [InlineData(GraphLinkRouting.Bezier, 400)]
    [InlineData(GraphLinkRouting.Bezier, -300)]
    [InlineData(GraphLinkRouting.Straight, 400)]
    [InlineData(GraphLinkRouting.Straight, -300)]
    [InlineData(GraphLinkRouting.Orthogonal, 400)]
    [InlineData(GraphLinkRouting.Orthogonal, -300)]
    public void RoutesEncloseTheirGeometryAndReachBothEndpoints(GraphLinkRouting routing, double dx)
    {
        var link = new GraphConnector(new GraphPoint(100, 50), new GraphPoint(100 + dx, 210), routing);
        Assert.Equal(link.Source, link.Point(-1));
        Assert.Equal(link.Target, link.Point(2));
        foreach (var point in link.Polyline())
        {
            Assert.True(link.Bounds.Contains(new GraphBounds(point.X, point.Y, 0, 0)));
        }
        for (var i = 0; i <= 20; i++)
        {
            var point = link.Point(i / 20d);
            Assert.True(link.Bounds.Contains(new GraphBounds(point.X, point.Y, 0, 0)));
            // Curves are hit-tested against a fine polyline; the error stays well below a logical pixel.
            Assert.InRange(link.Distance(point), 0, routing == GraphLinkRouting.Bezier ? 0.5 : 1e-9);
        }
        Assert.True(link.Length >= Math.Sqrt(dx * dx + 160 * 160) - 1e-9);
        Assert.True(link.Distance(new GraphPoint(100, 5000)) > 1000);
    }

    [Fact]
    public void StraightAndOrthogonalRoutesUseExactSegments()
    {
        var straight = new GraphConnector(default, new GraphPoint(30, 40), GraphLinkRouting.Straight);
        Assert.Equal(50, straight.Length, 9);
        Assert.Equal(new GraphPoint(15, 20), straight.Point(0.5));
        Assert.Equal(2, straight.Polyline().Length);
        var forward = new GraphConnector(default, new GraphPoint(200, 100), GraphLinkRouting.Orthogonal);
        Assert.Equal([default, new(100, 0), new(100, 100), new(200, 100)], forward.Polyline());
        Assert.Equal(300, forward.Length, 9);
        Assert.Equal(new GraphPoint(100, 50), forward.Point(0.5));
        var backward = new GraphConnector(new GraphPoint(200, 0), new GraphPoint(0, 100), GraphLinkRouting.Orthogonal);
        Assert.Equal(6, backward.Polyline().Length);
        Assert.Equal(new GraphPoint(224, 50), backward.Polyline()[2]);
        Assert.Equal(new GraphPoint(-24, 50), backward.Polyline()[3]);
        var empty = new GraphConnector(default, default, GraphLinkRouting.Straight);
        Assert.Equal(default, empty.Point(0.5));
        Assert.Equal(0, empty.Distance(default));
    }

    [Fact]
    public void StyledLinksDrawDashesAndArrowsAtTheTarget()
    {
        using var harness = new FrameHarness(220, 120);
        var canvas = new GraphCanvas { ShowGrid = false };
        var dashed = new GraphConnector(new GraphPoint(10, 30), new GraphPoint(190, 30), GraphLinkRouting.Straight);
        var shifted = new GraphConnector(new GraphPoint(10, 80), new GraphPoint(190, 80), GraphLinkRouting.Straight);
        var routed = new GraphConnector(new GraphPoint(10, 100), new GraphPoint(190, 100), GraphLinkRouting.Orthogonal);
        var degenerate = new GraphConnector(new GraphPoint(5, 5), new GraphPoint(5, 5), GraphLinkRouting.Straight);
        var links = new Dictionary<string, (GraphConnector Connector, GraphLinkStyle Style)>
        {
            ["dashed"] = (dashed, new GraphLinkStyle(Color.Red, 3, Arrow: true, Dash: 10)),
            ["shifted"] = (shifted, new GraphLinkStyle(Color.Red, 3, Dash: 10, DashOffset: 5)),
            ["routed"] = (routed, new GraphLinkStyle(Color.Red, 2, Arrow: true, Dash: 7, DashOffset: -3)),
            ["degenerate"] = (degenerate, new GraphLinkStyle(Color.Red, Arrow: true)),
            ["plain"] = (new GraphConnector(new GraphPoint(10, 55), new GraphPoint(190, 55), GraphLinkRouting.Straight),
                new GraphLinkStyle(Color.Red, 3))
        };
        var items = links.Select(static pair => new GraphElementView(pair.Key, GraphElementRole.Edge,
            pair.Value.Connector.Bounds, pair.Key)).ToArray();
        void Draw(Gui gui) => canvas.Draw(gui, items, (g, item) =>
        {
            var (connector, style) = links[item.Key];
            if (style.Dash == 0 && !style.Arrow)
            {
                connector.Draw(g, style.Color, style.Thickness);
            }
            else
            {
                connector.Draw(g, style);
            }
        }, id: "links");
        harness.Frame(Draw);
        harness.Frame(Draw);
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.True(IsRed(pixels.GetPixel(15, 30)));
        Assert.False(IsRed(pixels.GetPixel(25, 30)));
        Assert.True(IsRed(pixels.GetPixel(186, 30)));
        Assert.True(IsRed(pixels.GetPixel(40, 80)));
        Assert.False(IsRed(pixels.GetPixel(30, 80)));
        Assert.True(IsRed(pixels.GetPixel(100, 55)));
        Assert.False(IsRed(pixels.GetPixel(100, 62)));
    }

    [Fact]
    public void DomainPresentationIsLimitedToKnownNodesAndFollowsTheLevelOfDetail()
    {
        using var harness = new FrameHarness(900, 700);
        var fixture = new PresentationFixture();
        var view = fixture.View;
        void Draw(Gui gui) => view.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(GraphLevelOfDetail.Full, view.Canvas.Detail);
        Assert.True(fixture.Presented > 0);
        Assert.Contains(fixture.Visuals, visual => visual.Node.Id == fixture.Known);
        Assert.DoesNotContain(fixture.Visuals, visual => visual.Node.Id == fixture.Unknown);
        Assert.DoesNotContain(fixture.Unknown, fixture.Badged);
        Assert.True(fixture.Links > 0);
        Assert.Equal(56 + 2 * 28 + 40 + 8, view.Nodes.Single(node => node.Key == fixture.Known.ToString()).Bounds.Height);
        Assert.True(view.Nodes.Single(node => node.Key == fixture.Unknown.ToString()).Bounds.Height < 160);
        Assert.Equal(4, fixture.Shapes.Distinct().Count());
        view.Canvas.Camera.SetView(default, 0.45);
        fixture.Reset();
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(GraphLevelOfDetail.Compact, view.Canvas.Detail);
        Assert.Equal(0, fixture.Presented);
        Assert.NotEmpty(fixture.Shapes);
        view.Canvas.Camera.SetView(default, 0.2);
        fixture.Reset();
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(GraphLevelOfDetail.Outline, view.Canvas.Detail);
        Assert.Empty(fixture.Shapes);
        Assert.Equal(GraphLevelOfDetail.Outline, Assert.Single(fixture.LinkDetails.Distinct()));
        var port = view.Editor.Document.GetNode(fixture.Known).Ports[0].Id;
        harness.Click(Draw, view.Canvas.WorldToScreen(view.PortPosition(port)));
        Assert.Null(view.Canvas.Navigation.ConnectionSourceKey);
    }

    [Fact]
    public void ValidationMarksTheNodeOwningADiagnosedElement()
    {
        using var harness = new FrameHarness(900, 700);
        var scene = new SandboxScene();
        var view = scene.View;
        var node = scene.Graph.Editor.Document.Nodes.First();
        var other = scene.Graph.Editor.Document.Nodes.Last();
        view.Validation = [new Diagnostic("MG9001", GraphSeverity.Info, node.Id, "note"),
            new Diagnostic("MG9002", GraphSeverity.Warning, node.Ports[0].Id, "port"),
            new Diagnostic("MG9003", GraphSeverity.Error, other.Id, "node"),
            new Diagnostic("MG9004", GraphSeverity.Error, null, "graph")];
        void Draw(Gui gui) => view.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(GraphSeverity.Warning, view.NodeSeverity(node.Id));
        Assert.Equal(GraphSeverity.Error, view.NodeSeverity(other.Id));
        Assert.Null(view.NodeSeverity(scene.Graph.Editor.Document.Nodes.ElementAt(2).Id));
        view.Theme = GraphTheme.Light;
        harness.Frame(Draw);
        Assert.Same(GraphTheme.Light.Canvas, view.Canvas.Style);
    }

    [Fact]
    public void HeaderBadgesSitOnTheTopRightCornerOfTheirNode()
    {
        using var harness = new FrameHarness(1100, 700);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        scene.View.Theme = GraphTheme.Light;
        void Draw(Gui gui) => scene.View.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        var result = scene.Graph.Editor.Document.Nodes.Single(static node => node.TypeId == ArithmeticGraph.Result.Id);
        var bounds = scene.View.Nodes.Single(card => card.Key == result.Id.ToString()).Bounds;
        var corner = scene.Canvas.WorldToScreen(new GraphPoint(bounds.Right - 13, bounds.Y + 4));
        using var image = harness.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        var badge = pixels.GetPixel((int)corner.X, (int)corner.Y);
        Assert.True(badge.Red < 110 && badge.Green < 110, $"Expected the dark badge, found {badge}.");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2)]
    public void LinkHitTestingUsesScreenToleranceAndOnlyVisibleLinks(double zoom)
    {
        using var harness = new FrameHarness(1200, 900);
        var scene = new SandboxScene();
        scene.Canvas.ShowMinimap = false;
        scene.Canvas.Camera.SetView(default, zoom);
        void Draw(Gui gui) => scene.View.Draw(gui, "sandbox-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var edge = scene.Graph.Editor.Document.Edges.First();
        var middle = scene.View.Connector(edge.Id).Point(0.5);
        var near = new GraphPoint(middle.X, middle.Y + 7 / zoom);
        var far = new GraphPoint(middle.X, middle.Y + 30 / zoom);
        var tolerance = scene.View.LinkHitTolerance / zoom;
        Assert.Equal(edge.Id.ToString(), scene.View.HitLink(near, tolerance));
        Assert.Null(scene.View.HitLink(far, tolerance));
        harness.Click(Draw, scene.Canvas.WorldToScreen(near));
        Assert.Equal([edge.Id.ToString()], scene.Canvas.Navigation.SelectedKeys);
        scene.Canvas.Camera.SetView(new GraphPoint(50_000, 50_000), zoom);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.DoesNotContain(scene.Canvas.VisibleElements, static item => item.Role == GraphElementRole.Edge);
        Assert.Null(scene.View.HitLink(middle, tolerance));
    }

    [Fact]
    public void OverlaysRunInBothPassesWithTheFrameCameraMapping()
    {
        using var harness = new FrameHarness(600, 400);
        var canvas = new GraphCanvas { ShowGrid = false };
        canvas.Camera.SetView(new GraphPoint(-10, -20), 2);
        var overlay = new RecordingOverlay();
        canvas.Overlays.Add(overlay);
        GraphElementView[] items = [new("a", GraphElementRole.Node, new GraphBounds(0, 0, 40, 30), "A")];
        void Draw(Gui gui) => canvas.Draw(gui, items, id: "overlay-canvas");
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Contains(overlay.Contexts, static context => context.Pass == Pass.Pass1Build);
        var render = overlay.Contexts.Last(static context => context.Pass == Pass.Pass2Render);
        Assert.Equal(2, render.Zoom);
        Assert.Equal(new Vector2(20, 40), render.Offset(default));
        Assert.Equal(canvas.WorldToScreen(new GraphPoint(5, 5)), render.ToLocal(new GraphPoint(5, 5)));
        Assert.Same(canvas, render.Canvas);
    }

    [Fact]
    public async Task FlowScenarioObservesTheRealGraphOffTheUiThread()
    {
        using var harness = new FrameHarness(1100, 900);
        var playground = new GraphPlayground();
        playground.SelectScenario(1);
        Action<Gui> draw = playground.Draw;
        var flow = playground.Flow;
        harness.Frame(draw);
        await flow.Flow.Completion;
        harness.Frame(draw);
        harness.Frame(draw);
        var document = flow.Sandbox.Graph.Editor.Document;
        var result = document.Nodes.Single(static node => node.TypeId == ArithmeticGraph.Result.Id);
        Assert.Equal(6f, flow.Flow.Values[result.GetPort(ArithmeticGraph.ValueKey).Id]);
        Assert.Null(flow.Flow.Error);
        Assert.Empty(flow.Sandbox.View.Validation);
        flow.Sandbox.Canvas.Navigation.Select(result.Id.ToString());
        var output = flow.Sandbox.View.PortPosition(result.GetPort(ArithmeticGraph.ValueKey).Id);
        harness.Click(draw, flow.Sandbox.Canvas.WorldToScreen(output) + new Vector2(16, -17));
        Assert.Empty(flow.Sandbox.Canvas.Navigation.SelectedKeys);
        var numbers = document.Nodes.Where(static node => node.TypeId == ArithmeticGraph.Number.Id).ToArray();
        var number = numbers[0];
        var before = (float)number.Payload!;
        var bounds = flow.Sandbox.View.Nodes.Single(card => card.Key == number.Id.ToString()).Bounds;
        harness.Click(draw, flow.Sandbox.Canvas.WorldToScreen(new GraphPoint(bounds.X + 193, bounds.Y + 91)));
        harness.Frame(draw);
        Assert.Equal(before + 1, document.GetNode(number.Id).Payload);
        harness.Frame(draw);
        await flow.Flow.Completion;
        harness.Frame(draw);
        Assert.Equal((before + 1) * (float)numbers[1].Payload!,
            flow.Flow.Values[result.GetPort(ArithmeticGraph.ValueKey).Id]);
        Assert.True(flow.Flow.Revision >= 2);
        Assert.True(flow.Sandbox.View.Undo().Succeeded);
        Assert.Equal(before, document.GetNode(number.Id).Payload);
        flow.Flow.Animate = false;
        flow.Sandbox.Canvas.Camera.SetView(default, 0.2);
        harness.Frame(draw);
        harness.Frame(draw);
        Assert.True(flow.Sandbox.Graph.Editor.Execute(new SetPayloadCommand(number.Id, "not a number")).Succeeded);
        harness.Frame(draw);
        await flow.Flow.Completion;
        harness.Frame(draw);
        Assert.IsType<InvalidCastException>(flow.Flow.Error);
        Assert.Empty(flow.Flow.Values);
        // A rule-free core editor admits a data cycle, which only the dataflow compiler rejects.
        var add = document.GetNode(flow.Sandbox.View.AddNode(ArithmeticGraph.Add, default));
        Assert.True(new GraphEditor(document).Connect(add.GetPort(ArithmeticGraph.ValueKey).Id,
            add.GetPort(ArithmeticGraph.AKey).Id).Succeeded);
        harness.Frame(draw);
        Assert.Contains(flow.Sandbox.View.Validation, static d => d.Severity == GraphSeverity.Error);
        Assert.Equal(GraphSeverity.Error, flow.Sandbox.View.NodeSeverity(add.Id));
        Assert.Equal(GraphSeverity.Error,
            flow.Sandbox.View.EdgeSeverity(document.GetEdges(add.GetPort(ArithmeticGraph.AKey).Id).Single().Id));
        Assert.Empty(flow.Flow.Values);
        harness.Frame(draw);
    }

    [Fact]
    public async Task FlowOverlayReportsFailuresAndNeverOverlapsEvaluations()
    {
        var view = new SandboxScene().View;
        var overlay = new GraphFlowOverlay(view);
        var layout = new InstanceLayoutBuilder().Build();
        var instances = new InstanceBuffer(layout);
        var instance = instances.Allocate();
        var gate = new TaskCompletionSource();
        var slow = new FakeGraph(layout, gate.Task, static observations =>
            observations.Add(new GraphObservation(ElementId.New(), 4)));
        var published = 0;
        Assert.True(overlay.TryObserve(slow, new EvaluationContext(), instances, instance, () => published++));
        Assert.True(overlay.Running);
        Assert.False(overlay.TryObserve(slow, new EvaluationContext(), instances, instance));
        gate.SetResult();
        await overlay.Completion;
        Assert.False(overlay.Running);
        Assert.Equal(1, published);
        Assert.Equal(4, Assert.Single(overlay.Values).Value);
        Assert.Equal("4", overlay.Format(4));
        Assert.Equal("text", overlay.Format(new Text()));
        var failing = new FakeGraph(layout, Task.CompletedTask,
            static _ => throw new InvalidOperationException("observe failed"));
        Assert.True(overlay.TryObserve(failing, new EvaluationContext(), instances, instance));
        await overlay.Completion;
        Assert.Equal("observe failed", overlay.Error!.Message);
        Assert.Empty(overlay.Values);
        overlay.Publish([new GraphObservation(ElementId.New(), null!)]);
        Assert.Empty(overlay.Values);
    }

    static bool IsRed(SKColor color) => color.Red > 180 && color.Green < 90 && color.Blue < 90;

    sealed class Text
    {
        public override string ToString() => "text";
    }

    sealed class FakeGraph(InstanceLayout layout, Task evaluation, Action<ICollection<GraphObservation>> observe)
        : ICompiledGraph
    {
        public StableId AssetId => default;
        public InstanceLayout InstanceLayout => layout;
        public IReadOnlyList<Type> RequiredFeatures => [];
        public IReadOnlyList<ElementId> Outputs => [];
        public void Initialize(EvaluationContext context, InstanceBuffer instances, InstanceHandle instance) { }
        public ValueTask EvaluateAsync(EvaluationContext context, InstanceBuffer instances, InstanceHandle instance) =>
            new(evaluation);
        public T ReadOutput<T>(EvaluationContext context, InstanceBuffer instances, InstanceHandle instance,
            ElementId output) where T : unmanaged => throw new NotSupportedException();
        public T? ReadOutputReference<T>(EvaluationContext context, InstanceBuffer instances, InstanceHandle instance,
            ElementId output) where T : class => throw new NotSupportedException();
        public bool Raise(InstanceBuffer instances, InstanceHandle instance, StableId signal) => false;
        public bool SetVariable(InstanceBuffer instances, InstanceHandle instance, ElementId variable, object? value) =>
            false;
        public bool SetVariable<T>(InstanceBuffer instances, InstanceHandle instance, ElementId variable, T value)
            where T : unmanaged => false;
        public void Observe(EvaluationContext context, InstanceBuffer instances, InstanceHandle instance,
            ICollection<GraphObservation> observations) => observe(observations);
    }

    sealed class RecordingOverlay : IGraphOverlay
    {
        public List<GraphOverlayContext> Contexts { get; } = [];
        public void Draw(Gui gui, GraphOverlayContext context) => Contexts.Add(context);
    }

    sealed class PresentationFixture
    {
        static readonly PortType Value = new(StableId.New(), "value");
        static readonly NodeType Card = Type("Card");
        static readonly NodeType Missing = Type("Missing");

        public PresentationFixture()
        {
            var editor = new GraphEditor(new GraphDocument(Registry(Card, Missing)));
            var known = new AddNodeCommand(Card);
            var target = new AddNodeCommand(Card);
            var unknown = new AddNodeCommand(Missing);
            editor.Execute(known);
            editor.Execute(target);
            editor.Execute(unknown);
            editor.Execute(new MoveNodeCommand(target.NodeId, new MASS4.Graph.GraphPoint(400, 0)));
            editor.Execute(new MoveNodeCommand(unknown.NodeId, new MASS4.Graph.GraphPoint(0, 300)));
            editor.Connect(editor.Document.GetNode(known.NodeId).Ports[2].Id,
                editor.Document.GetNode(target.NodeId).Ports[0].Id);
            var loaded = GraphJson.Deserialize(GraphJson.Serialize(editor.Document), Registry(Card)).Document!;
            Known = known.NodeId;
            Unknown = unknown.NodeId;
            var shape = 0;
            View = new GraphDocumentView(new GraphEditor(loaded))
            {
                ContentHeight = _ => 40,
                NodePresenter = (_, visual) =>
                {
                    Presented++;
                    Visuals.Add(visual);
                },
                NodeBadges = node =>
                {
                    Badged.Add(node.Id);
                    return ["beta"];
                },
                PortShape = _ =>
                {
                    var result = (GraphPortShape)(shape++ % 4);
                    Shapes.Add(result);
                    return result;
                },
                LinkRenderer = (gui, link) =>
                {
                    Links++;
                    LinkDetails.Add(link.Detail);
                    link.Connector.Draw(gui, link.Style);
                }
            };
        }

        public GraphDocumentView View { get; }
        public ElementId Known { get; }
        public ElementId Unknown { get; }
        public int Presented { get; private set; }
        public int Links { get; private set; }
        public List<GraphNodeVisual> Visuals { get; } = [];
        public List<ElementId> Badged { get; } = [];
        public List<GraphPortShape> Shapes { get; } = [];
        public List<GraphLevelOfDetail> LinkDetails { get; } = [];

        public void Reset()
        {
            Presented = 0;
            Links = 0;
            Visuals.Clear();
            Shapes.Clear();
            LinkDetails.Clear();
        }

        static NodeType Type(string name) => new(StableId.New(), name,
        [
            PortDeclaration.Input(StableId.New(), "In A", Value.Id),
            PortDeclaration.Input(StableId.New(), "In B", Value.Id),
            PortDeclaration.Output(StableId.New(), "Out A", Value.Id),
            PortDeclaration.Output(StableId.New(), "Out B", Value.Id)
        ]);

        static GraphTypeRegistry Registry(params NodeType[] types)
        {
            var registry = new GraphTypeRegistry();
            registry.PortTypes.Register(Value);
            foreach (var type in types)
            {
                registry.NodeTypes.Register(type);
            }
            return registry;
        }
    }
}

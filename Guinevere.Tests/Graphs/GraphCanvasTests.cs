using Guinevere.Graphs;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Graphs;

public sealed class GraphCanvasTests
{
    [Fact]
    public void TenThousandNodesBuildOnlyVisibleControlsWithStableIdentitiesAcrossPasses()
    {
        using var harness = new FrameHarness();
        var canvas = new GraphCanvas { ShowMinimap = true };
        var items = Enumerable.Range(0, 10000).Select(static i =>
            new GraphElementView($"tile{i}", GraphElementRole.Node,
                new GraphBounds(i % 100 * 200, i / 100 * 120, 100, 80), $"Tile {i}")).ToArray();
        var build = new Dictionary<string, string>();
        var render = new Dictionary<string, string>();
        LayoutNode? surface = null;
        void Draw(Gui gui)
        {
            surface = canvas.Draw(gui, items, (g, item) =>
            {
                (g.Pass == Pass.Pass1Build ? build : render).Add(item.Key, g.CurrentNode.Id);
                g.DrawRectFilled(g.CurrentNode.Rect, Color.SlateBlue);
            }, id: "canvas");
        }
        harness.Frame(Draw);
        Assert.Empty(build);
        Assert.Empty(render);
        harness.Frame(Draw);
        Assert.InRange(canvas.VisibleCount, 1, 20);
        Assert.Equal(build.OrderBy(static entry => entry.Key), render.OrderBy(static entry => entry.Key));
        Assert.InRange(surface!.ChildNodes.Count, 1, 21);
        var tile0 = build["tile0"];
        build.Clear();
        render.Clear();
        canvas.Camera.Pan(new Vector2(-50, 0));
        harness.Frame(Draw);
        Assert.Equal(tile0, build["tile0"]);
        Assert.Equal(build.OrderBy(static entry => entry.Key), render.OrderBy(static entry => entry.Key));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(16)]
    public void KeyboardOnlyNavigationRevealsOffscreenNodesAndDelegatesConnections(double zoom)
    {
        using var harness = new FrameHarness();
        var canvas = new GraphCanvas();
        canvas.Camera.SetView(default, zoom);
        var items = GraphNavigationTests.Catalog();
        void Draw(Gui gui) => canvas.Draw(gui, items, id: "graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Gui.Focus.RequestFocus("graph");
        harness.Frame(Draw);
        Key(harness, Draw, KeyboardKey.Right);
        Assert.Equal("a", canvas.Navigation.FocusedKey);
        Key(harness, Draw, KeyboardKey.Enter);
        Assert.Equal("out", canvas.Navigation.FocusedKey);
        Key(harness, Draw, KeyboardKey.Enter);
        Key(harness, Draw, KeyboardKey.Right);
        Assert.Equal("b", canvas.Navigation.FocusedKey);
        Assert.True(canvas.VisibleBounds.Intersects(items[1].Bounds));
        Key(harness, Draw, KeyboardKey.Enter);
        Assert.Equal("in", canvas.Navigation.FocusedKey);
        GraphEditRequest? request = null;
        canvas.Navigation.EditRequested = edit =>
        {
            request = edit;
            return new GraphEditResult(true, "Connected.");
        };
        Key(harness, Draw, KeyboardKey.Enter);
        Assert.Equal(new GraphEditRequest(GraphEditKind.Connect, "out", "in"), request);
        Assert.Null(canvas.Navigation.ConnectionSourceKey);
        Key(harness, Draw, KeyboardKey.Tab);
        Assert.Equal("extra", canvas.Navigation.FocusedKey);
        Key(harness, Draw, KeyboardKey.Tab, KeyboardKey.LeftShift);
        Assert.Equal("in", canvas.Navigation.FocusedKey);
        Key(harness, Draw, KeyboardKey.Space);
        Assert.Contains("in", canvas.Navigation.SelectedKeys);
        Key(harness, Draw, KeyboardKey.Delete);
        Assert.Equal(new GraphEditRequest(GraphEditKind.Delete, "in"), request);
        Key(harness, Draw, KeyboardKey.Escape);
        Assert.Equal("b", canvas.Navigation.FocusedKey);
        Key(harness, Draw, KeyboardKey.Right, KeyboardKey.LeftAlt);
        Assert.Equal("edge", canvas.Navigation.FocusedKey);
        Key(harness, Draw, KeyboardKey.Delete);
        Assert.Equal(new GraphEditRequest(GraphEditKind.Delete, "edge"), request);
        Key(harness, Draw, KeyboardKey.Escape);
        Assert.Equal("out", canvas.Navigation.FocusedKey);
    }

    [Fact]
    public void WheelAndCapturedMiddleDragUseCanvasCoordinates()
    {
        using var harness = new FrameHarness();
        var canvas = new GraphCanvas();
        void Draw(Gui gui) => canvas.Draw(gui, [], id: "graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var anchor = new Vector2(150, 100);
        var before = canvas.Camera.ViewToWorld(anchor);
        harness.Input.MoveTo(anchor);
        harness.Input.Scroll(1);
        harness.Frame(Draw);
        Assert.Equal(1.15, canvas.Camera.Zoom, 8);
        Assert.Equal(before.X, canvas.Camera.ViewToWorld(anchor).X, 8);
        Assert.Equal(before.Y, canvas.Camera.ViewToWorld(anchor).Y, 8);
        var origin = canvas.Camera.Origin;
        harness.Input.PressButton(MouseButton.Middle);
        harness.Frame(Draw);
        harness.Input.MoveTo(180, 120);
        harness.Frame(Draw);
        Assert.Equal(origin.X - 30 / 1.15, canvas.Camera.Origin.X, 8);
        Assert.Equal(origin.Y - 20 / 1.15, canvas.Camera.Origin.Y, 8);
        harness.Input.MoveTo(700, 400);
        harness.Frame(Draw);
        Assert.Equal(origin.X - 550 / 1.15, canvas.Camera.Origin.X, 8);
        harness.Input.ReleaseButton(MouseButton.Middle);
        harness.Frame(Draw);
        harness.Input.PressButton(MouseButton.Left);
        harness.Input.MoveTo(710, 410);
        harness.Frame(Draw);
        Assert.Equal(origin.X - 550 / 1.15, canvas.Camera.Origin.X, 8);
    }

    [Fact]
    public void CameraShortcutsAndPaletteHooksWorkWithoutPointer()
    {
        using var harness = new FrameHarness();
        var canvas = new GraphCanvas { ShowGrid = false, ShowAxes = false };
        var searches = new List<GraphSearchKind>();
        canvas.Navigation.SearchRequested = searches.Add;
        void Draw(Gui gui) => canvas.Draw(gui, GraphNavigationTests.Catalog(), id: "graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Gui.Focus.RequestFocus("graph");
        harness.Frame(Draw);
        Key(harness, Draw, KeyboardKey.Equal);
        Assert.Equal(1.25, canvas.Camera.Zoom);
        Key(harness, Draw, KeyboardKey.Minus);
        Assert.Equal(1, canvas.Camera.Zoom);
        Key(harness, Draw, KeyboardKey.KeyPadAdd);
        Key(harness, Draw, KeyboardKey.KeyPadSubtract);
        var origin = canvas.Camera.Origin;
        Key(harness, Draw, KeyboardKey.Left, KeyboardKey.LeftControl);
        Assert.Equal(origin.X - 48, canvas.Camera.Origin.X);
        Key(harness, Draw, KeyboardKey.Right, KeyboardKey.LeftControl);
        Key(harness, Draw, KeyboardKey.Up, KeyboardKey.LeftControl);
        Key(harness, Draw, KeyboardKey.Down, KeyboardKey.LeftControl);
        Key(harness, Draw, KeyboardKey.D0);
        Assert.Equal(default, canvas.Camera.Origin);
        Key(harness, Draw, KeyboardKey.Home);
        Assert.True(canvas.Camera.Zoom < 1);
        Key(harness, Draw, KeyboardKey.Right);
        Key(harness, Draw, KeyboardKey.Space);
        Key(harness, Draw, KeyboardKey.F);
        Assert.True(canvas.Camera.Zoom > 1);
        Key(harness, Draw, KeyboardKey.K, KeyboardKey.LeftControl);
        Key(harness, Draw, KeyboardKey.Insert);
        Key(harness, Draw, KeyboardKey.A, KeyboardKey.LeftShift);
        Assert.Equal([GraphSearchKind.Commands, GraphSearchKind.CreateNode, GraphSearchKind.CreateNode], searches);
        Key(harness, Draw, KeyboardKey.F10);
    }

    [Fact]
    public void AccessibilityIncludesVirtualizedRolesRelationshipsAndValidationAnnouncements()
    {
        using var harness = new FrameHarness();
        var sink = new GraphSink();
        harness.Gui.Platform.Register<IAccessibilityCapability>(sink);
        var canvas = new GraphCanvas { Name = "Material graph" };
        var items = GraphNavigationTests.Catalog();
        void Draw(Gui gui)
        {
            sink.Elements.Clear();
            sink.Controls.Clear();
            canvas.Draw(gui, items, id: "graph");
        }
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(items.Length + 1, sink.Elements.Count);
        var graph = Assert.Single(sink.Elements, static element => element.View.Role == GraphElementRole.Graph);
        Assert.Equal("Material graph", graph.View.Name);
        var node = Assert.Single(sink.Elements, static element => element.View.Key == "b");
        Assert.True(node.Virtualized);
        Assert.Equal(graph.Id, node.ParentId);
        var port = Assert.Single(sink.Elements, static element => element.View.Key == "in");
        Assert.Equal(node.Id, port.ParentId);
        Assert.Equal("float", port.View.TypeName);
        Assert.Equal(items.Length, port.Count);
        var edge = Assert.Single(sink.Elements, static element => element.View.Role == GraphElementRole.Edge);
        Assert.Equal(port.Id, edge.TargetId);
        Assert.EndsWith("/graph/out", edge.SourceId);
        Assert.False(edge.Virtualized);
        var id = node.Id;
        canvas.Navigation.Focus("b");
        canvas.Navigation.ToggleSelection();
        harness.Gui.Focus.RequestFocus("graph");
        harness.Frame(Draw);
        node = Assert.Single(sink.Elements, static element => element.View.Key == "b");
        Assert.Equal(id, node.Id);
        Assert.True(node.Focused);
        Assert.True(node.Selected);
        Assert.False(node.Virtualized);
        canvas.Navigation.Focus("out");
        canvas.Navigation.Activate();
        canvas.Navigation.Focus("in");
        canvas.Navigation.Activate();
        harness.Frame(Draw);
        Assert.Contains(sink.Events, static change => change.Kind == GraphSemanticEventKind.ValidationFailed);
        Assert.Contains(sink.Events, static change => change.Kind == GraphSemanticEventKind.SelectionChanged);
        Assert.Equal(items.Length + 1, sink.Controls.Count);
    }

    [Fact]
    public void MinimapClickRecentersAndFitHandlesEmptySelection()
    {
        using var harness = new FrameHarness();
        var canvas = new GraphCanvas { ShowMinimap = true };
        void Draw(Gui gui) => canvas.Draw(gui, GraphNavigationTests.Catalog(), id: "graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        var origin = canvas.Camera.Origin;
        canvas.FitSelection();
        Assert.Equal(origin, canvas.Camera.Origin);
        harness.Click(Draw, new Vector2(300, 230));
        Assert.NotEqual(origin, canvas.Camera.Origin);
        harness.Input.MoveTo(300, 230);
        harness.Input.PressButton();
        harness.Frame(Draw);
        harness.Input.MoveTo(320, 240);
        harness.Frame(Draw);
        harness.Input.MoveTo(330, 240);
        harness.Frame(Draw);
        harness.Input.ReleaseButton();
        harness.Frame(Draw);
        canvas.Navigation.Focus("a");
        void Empty(Gui gui) => canvas.Draw(gui, [], id: "graph");
        harness.Frame(Empty);
        canvas.FitAll();
        Assert.Equal(default, canvas.Camera.Origin);
        Assert.Equal(1, canvas.Camera.Zoom);
    }

    [Fact]
    public void SemanticInputIsAppliedOnceAtTheNextBuildBoundary()
    {
        using var harness = new FrameHarness();
        var source = Substitute.For<ISemanticInputCapability>();
        source.ActiveDevice.Returns(new InputDevice("pad", InputDeviceKind.Controller));
        harness.Gui.Platform.Register(source);
        var canvas = new GraphCanvas();
        void Draw(Gui gui) => canvas.Draw(gui, GraphNavigationTests.Catalog(), id: "graph");
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Gui.Focus.RequestFocus("graph");
        harness.Frame(Draw);
        source.IsPressed(SemanticAction.Right).Returns(true);
        harness.Frame(Draw);
        Assert.Null(canvas.Navigation.FocusedKey);
        source.IsPressed(SemanticAction.Right).Returns(false);
        harness.Frame(Draw);
        Assert.Equal("a", canvas.Navigation.FocusedKey);
        source.IsPressed(SemanticAction.Accept).Returns(true);
        harness.Frame(Draw);
        source.IsPressed(SemanticAction.Accept).Returns(false);
        harness.Frame(Draw);
        Assert.Equal("out", canvas.Navigation.FocusedKey);
        source.IsPressed(SemanticAction.Cancel).Returns(true);
        harness.Frame(Draw);
        source.IsPressed(SemanticAction.Cancel).Returns(false);
        harness.Frame(Draw);
        Assert.Equal("a", canvas.Navigation.FocusedKey);
        var activation = Substitute.For<IControlActivationSource>();
        harness.Gui.ControlActivation = activation;
        activation.IsActivated("graph").Returns(true);
        harness.Frame(Draw);
        activation.IsActivated("graph").Returns(false);
        harness.Frame(Draw);
        Assert.Equal("out", canvas.Navigation.FocusedKey);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ScaledHostKeepsPointerZoomAndElementDrawingInLogicalCoordinates(float density)
    {
        using var harness = new FrameHarness(500, 400);
        var canvas = new GraphCanvas { ShowGrid = false };
        canvas.Camera.SetView(default, 2);
        GraphElementView[] items =
        [
            new("tile", GraphElementRole.Node, new GraphBounds(50, 20, 50, 30), "Tile")
        ];
        void Draw(Gui gui)
        {
            gui.DrawBackgroundRect(Color.Black);
            var parent = gui.Node(200, 150);
            using var scope = parent.Enter();
            gui.SetTransform(Matrix3x2.CreateScale(density, parent.Rect.Position));
            canvas.Draw(gui, items, static (g, _) => g.DrawRectFilled(g.CurrentNode.Rect, Color.Red), id: "graph");
        }
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.Equal(new Vector2(120, 60) * density, canvas.WorldToScreen(new GraphPoint(60, 30)));
        Assert.Equal(new GraphPoint(60, 30), canvas.ScreenToWorld(new Vector2(120, 60) * density));
        using (var image = harness.Snapshot())
        using (var pixels = SKBitmap.FromImage(image))
        {
            Assert.Equal(SKColors.Red, pixels.GetPixel((int)(120 * density), (int)(60 * density)));
            Assert.Equal(SKColors.Black, pixels.GetPixel((int)(210 * density), (int)(60 * density)));
        }
        harness.Click(Draw, new Vector2(120, 60) * density);
        Assert.Equal("tile", canvas.Navigation.FocusedKey);
        var anchor = new Vector2(120, 60);
        var before = canvas.Camera.ViewToWorld(anchor);
        harness.Input.Scroll(1);
        harness.Frame(Draw);
        Assert.Equal(before.X, canvas.Camera.ViewToWorld(anchor).X, 8);
        Assert.Equal(before.Y, canvas.Camera.ViewToWorld(anchor).Y, 8);
    }

    [Fact]
    public void SingularHostTransformKeepsScreenToWorldFinite()
    {
        using var harness = new FrameHarness();
        var camera = new GraphCamera(0.5, 4);
        var canvas = new GraphCanvas(camera);
        void Draw(Gui gui)
        {
            using var scope = gui.Node(200, 150).Enter();
            gui.SetTransform(Matrix3x2.CreateScale(0));
            canvas.Draw(gui, [], id: "graph");
        }
        harness.Frame(Draw);
        Assert.Same(camera, canvas.Camera);
        Assert.Equal(new GraphPoint(30, 40), canvas.ScreenToWorld(new Vector2(30, 40)));
        Assert.Equal(Vector2.Zero, canvas.WorldToScreen(new GraphPoint(30, 40)));
    }

    [Fact]
    public void TabCanLeaveTheCanvasAndEmbeddedControlsKeepTheirKeyboardInput()
    {
        using var harness = new FrameHarness();
        var canvas = new GraphCanvas();
        void Draw(Gui gui)
        {
            canvas.Draw(gui, GraphNavigationTests.Catalog(), static (g, item) =>
            {
                if (item.Key == "a")
                {
                    using var child = g.Node(40, 20, "embedded").Enter();
                    g.Pressable();
                }
            }, width: 300, height: 200, id: "graph");
            using var outside = gui.Node(40, 20, "outside").Enter();
            gui.Pressable();
        }
        harness.Frame(Draw);
        harness.Frame(Draw);
        harness.Gui.Focus.RequestFocus("graph");
        harness.Frame(Draw);
        canvas.Navigation.Focus("a");
        Key(harness, Draw, KeyboardKey.Tab);
        Assert.NotEqual("graph", harness.Gui.Focus.CurrentFocusedId);
        harness.Gui.Focus.RequestFocus("embedded");
        harness.Frame(Draw);
        Key(harness, Draw, KeyboardKey.Enter);
        Assert.Equal("a", canvas.Navigation.FocusedKey);
    }

    [Fact]
    public void PackageUsesPssWithoutExcaliburOrEngineDependencies()
    {
        var references = typeof(GraphCanvas).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, static reference =>
            reference.Name!.Contains("Excalibur", StringComparison.Ordinal)
            || reference.Name.Contains("Turian", StringComparison.Ordinal)
            || reference.Name.Contains("Gaya", StringComparison.Ordinal));
        Assert.Contains(references, static reference => reference.Name == "Guinevere");
        Assert.Contains(references, static reference => reference.Name == "Guinevere.Styling");
    }

    internal static void Key(FrameHarness harness, Action<Gui> draw, KeyboardKey key, KeyboardKey? modifier = null)
    {
        if (modifier is { } held)
        {
            harness.Input.PressKey(held);
        }
        harness.Input.PressKey(key);
        harness.Frame(draw);
        harness.Input.ReleaseKey(key);
        if (modifier is { } released)
        {
            harness.Input.ReleaseKey(released);
        }
        harness.Frame(draw);
    }

    sealed class GraphSink : IGraphSemanticsSink, IAccessibilityCapability
    {
        public List<ControlSemantics> Controls { get; } = [];
        public List<GraphElementSemantics> Elements { get; } = [];
        public List<GraphSemanticEvent> Events { get; } = [];
        public void BeginFrame() { }
        public void EndFrame() { }
        public void Publish(ControlSemantics semantics) => Controls.Add(semantics);
        public void PublishGraph(GraphElementSemantics semantics) => Elements.Add(semantics);
        public void Announce(GraphSemanticEvent change) => Events.Add(change);
    }
}

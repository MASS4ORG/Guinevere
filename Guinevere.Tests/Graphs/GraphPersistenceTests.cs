using System.Text.Json.Nodes;
using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class GraphPersistenceTests
{
    [Fact]
    public void AuthoredLayoutAndCameraRoundTripSeparatelyWithoutTransientState()
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var first = view.Editor.Document.Nodes.First().Id;
        view.SetPosition(first, new GraphPoint(-123.5, 456.75));
        view.SelectAll();
        view.Canvas.Camera.SetView(new GraphPoint(-50, 90), 1.5);
        var authored = view.Serialize();
        var topology = view.Serialize(includeLayout: false);
        var layout = view.SerializeLayout();
        var camera = view.SerializeViewState();
        Assert.Null(JsonNode.Parse(topology)!["layout"]);
        Assert.DoesNotContain("selected", authored + layout + camera, StringComparison.OrdinalIgnoreCase);
        var loaded = GraphDocumentView.Load(authored, view.Editor.Document.Registry, viewState: camera);
        var restored = Assert.IsType<GraphDocumentView>(loaded.View);
        Assert.Empty(loaded.Diagnostics);
        Assert.Equal(authored, restored.Serialize());
        Assert.Equal(view.Position(first), restored.Position(first));
        Assert.Equal(view.Canvas.Camera.Origin, restored.Canvas.Camera.Origin);
        Assert.Equal(1.5, restored.Canvas.Camera.Zoom);
        Assert.Empty(restored.Canvas.Navigation.SelectedKeys);
        Assert.Null(restored.Canvas.Navigation.FocusedKey);
        Assert.False(restored.Editor.CanUndo);
        var sidecar = GraphDocumentView.Load(topology, view.Editor.Document.Registry, layout, camera).View!;
        Assert.Equal(authored, sidecar.Serialize());
        Assert.Equal(camera, sidecar.SerializeViewState());
        Assert.Equal(topology, sidecar.Serialize(includeLayout: false));
    }

    [Fact]
    public void UnknownPayloadPortsAndFutureExtensionsSurviveEditingAndRepeatedSaves()
    {
        var scene = new SandboxScene();
        var root = JsonNode.Parse(scene.View.Serialize())!;
        var node = root["nodes"]![0]!;
        var nodeId = ElementId.Parse(node["id"]!.GetValue<string>());
        node["type"] = StableId.New().ToString();
        node["payload"] = new JsonObject { ["future"] = new JsonArray(1, "text", true), ["nested"] = new JsonObject() };
        node["vendor"] = new JsonObject { ["opaque"] = 42 };
        root["vendor-root"] = new JsonArray("do not drop");
        root["layout"]!["vendor-layout"] = "keep";
        var loaded = GraphDocumentView.Load(root.ToJsonString(), scene.View.Editor.Document.Registry);
        var view = Assert.IsType<GraphDocumentView>(loaded.View);
        Assert.NotEmpty(loaded.Diagnostics);
        view.NodeTitle = n => n.Id == nodeId ? throw new InvalidOperationException("Unknown callback") : "Known";
        view.Canvas.Navigation.Select(nodeId.ToString());
        view.MoveSelection(new GraphPoint(20, 30));
        view.Refresh();
        var saved = view.Serialize();
        var after = JsonNode.Parse(saved)!;
        Assert.True(JsonNode.DeepEquals(node, after["nodes"]![0]));
        Assert.True(JsonNode.DeepEquals(root["vendor-root"], after["vendor-root"]));
        Assert.Equal("keep", after["layout"]!["vendor-layout"]!.GetValue<string>());
        var reloaded = GraphDocumentView.Load(saved, scene.View.Editor.Document.Registry).View!;
        Assert.Equal(saved, reloaded.Serialize());
        Assert.Equal(3, reloaded.Editor.Document.Edges.Count);
        Assert.Equal("Unknown node", reloaded.Nodes.Single(n => n.Key == nodeId.ToString()).Name);
        Assert.IsType<UnknownPayload>(reloaded.Editor.Document.GetNode(nodeId).Payload);
    }

    [Fact]
    public void RegistryMigrationRunsThroughTheCoreLoader()
    {
        var oldRegistry = new GraphTypeRegistry();
        var identity = StableId.New();
        var oldType = new NodeType(identity, "Old", []);
        oldRegistry.NodeTypes.Register(oldType);
        var original = new GraphDocumentView(new GraphEditor(new GraphDocument(oldRegistry)));
        var id = original.AddNode(oldType, new GraphPoint(20, 30));
        var registry = new GraphTypeRegistry();
        registry.NodeTypes.Register(new NodeType(identity, "Current", []) { Version = 2 });
        var called = false;
        registry.AddMigration(new NodeMigration(identity, 1, node =>
        {
            called = true;
            node["vendor"] = "migrated";
            return node;
        }));
        var loaded = GraphDocumentView.Load(original.Serialize(), registry);
        Assert.True(called);
        var view = Assert.IsType<GraphDocumentView>(loaded.View);
        Assert.Equal(2, view.Editor.Document.GetNode(id).TypeVersion);
        Assert.Equal("migrated", view.Editor.Document.GetNode(id).Extensions["vendor"].GetString());
        Assert.Equal(new GraphPoint(20, 30), view.Position(id));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("not JSON")]
    [InlineData("{\"format\":\"mass4.graph\",\"schemaVersion\":\"bad\"}")]
    public void InvalidInputProducesDiagnosticsAndNoPartialView(string json)
    {
        var loaded = GraphDocumentView.Load(json, new GraphTypeRegistry());
        Assert.Null(loaded.View);
        Assert.NotEmpty(loaded.Diagnostics);
    }

    [Fact]
    public void InvalidLayoutRecordsFailAtomicallyAndPositionExtensionsAreRetained()
    {
        var scene = new SandboxScene();
        var json = scene.View.Serialize();
        var root = JsonNode.Parse(scene.View.SerializeLayout())!;
        root["positions"]![0]!["vendor"] = "keep";
        var loaded = GraphDocumentView.Load(json, scene.View.Editor.Document.Registry, root.ToJsonString()).View!;
        Assert.Equal("keep", JsonNode.Parse(loaded.SerializeLayout())!["positions"]![0]!["vendor"]!.GetValue<string>());
        root["positions"]!.AsArray().Add(root["positions"]![0]!.DeepClone());
        Assert.Null(GraphDocumentView.Load(json, scene.View.Editor.Document.Registry, root.ToJsonString()).View);
        root["positions"] = new JsonArray((JsonNode?)null);
        Assert.Null(GraphDocumentView.Load(json, scene.View.Editor.Document.Registry, root.ToJsonString()).View);
        Assert.Equal(json, scene.View.Serialize());
    }

    [Fact]
    public void FutureKnownNodeVersionsNeverReachDomainPresenters()
    {
        var scene = new SandboxScene();
        var root = JsonNode.Parse(scene.View.Serialize())!;
        foreach (var node in root["nodes"]!.AsArray())
        {
            node!["version"] = 99;
        }
        var view = GraphDocumentView.Load(root.ToJsonString(), scene.View.Editor.Document.Registry).View!;
        view.NodeTitle = static _ => throw new InvalidOperationException();
        view.ContentHeight = static _ => throw new InvalidOperationException();
        view.NodeClasses = static _ => throw new InvalidOperationException();
        view.NodeBadges = static _ => throw new InvalidOperationException();
        view.NodePresenter = static (_, _) => throw new InvalidOperationException();
        view.PortShape = static _ => throw new InvalidOperationException();
        using var harness = new FrameHarness(1200, 800);
        void Draw(Gui gui) => view.Draw(gui);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.All(view.Nodes, static node => Assert.Equal("Unknown node", node.Name));
        var saved = JsonNode.Parse(view.Serialize())!;
        Assert.True(JsonNode.DeepEquals(root["nodes"], saved["nodes"]));
    }

    [Fact]
    public void FutureOrMismatchedSidecarsFailWithoutChangingTheSource()
    {
        var scene = new SandboxScene();
        var json = scene.View.Serialize();
        var layout = JsonNode.Parse(scene.View.SerializeLayout())!;
        layout["version"] = 2;
        Assert.Null(GraphDocumentView.Load(json, scene.View.Editor.Document.Registry, layout.ToJsonString()).View);
        layout["version"] = 1;
        layout["assetId"] = StableId.New().ToString();
        Assert.Null(GraphDocumentView.Load(json, scene.View.Editor.Document.Registry, layout.ToJsonString()).View);
        Assert.Null(GraphDocumentView.Load(json, scene.View.Editor.Document.Registry, viewState: "{}").View);
        Assert.Equal(json, scene.View.Serialize());
        Assert.False(scene.View.Editor.CanUndo);
    }
}

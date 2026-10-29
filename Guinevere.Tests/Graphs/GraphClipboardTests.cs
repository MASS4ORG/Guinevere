using System.Text.Json.Nodes;
using Example02;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class GraphClipboardTests
{
    [Fact]
    public void CopyPasteRemapsTopologyAndRestoresSelectionOnUndoAndRedo()
    {
        var scene = new SandboxScene();
        var view = scene.View;
        view.SelectAll();
        var original = scene.Graph.Editor.Document.Nodes.Select(static node => node.Id).ToHashSet();
        var json = view.CopySelection();
        Assert.True(view.Paste(json, new GraphPoint(32, 48)).Succeeded);
        Assert.Equal("Paste nodes", view.Editor.NextUndo!.Description);
        var document = view.Editor.Document;
        var copies = document.Nodes.Where(node => !original.Contains(node.Id)).ToArray();
        Assert.Equal(4, copies.Length);
        Assert.Equal(6, document.Edges.Count);
        Assert.Equal(copies.Select(static node => node.Id.ToString()).ToHashSet(), view.Canvas.Navigation.SelectedKeys);
        var copiedPorts = copies.SelectMany(static node => node.Ports).Select(static port => port.Id).ToHashSet();
        Assert.Equal(3, document.Edges.Count(edge => copiedPorts.Contains(edge.Source) && copiedPorts.Contains(edge.Target)));
        Assert.True(view.Undo().Succeeded);
        Assert.Equal(4, document.Nodes.Count);
        Assert.True(original.All(id => view.Canvas.Navigation.SelectedKeys.Contains(id.ToString())));
        Assert.True(view.Redo().Succeeded);
        Assert.Equal(copies.Select(static node => node.Id.ToString()).ToHashSet(), view.Canvas.Navigation.SelectedKeys);
        Assert.All(copies.Where(static node => node.TypeId == ArithmeticGraph.Number.Id),
            static node => Assert.IsType<float>(node.Payload));
        view.DeleteSelection();
        Assert.Equal(4, document.Nodes.Count);
        Assert.Empty(view.Canvas.Navigation.SelectedKeys);
        view.Undo();
        Assert.Equal(8, document.Nodes.Count);
        Assert.Equal(4, view.Canvas.Navigation.SelectedKeys.Count);
    }

    [Fact]
    public void CopyExcludesCrossingLinksAndCutWaitsForClipboardAcceptance()
    {
        var scene = new SandboxScene();
        var id = scene.Graph.Editor.Document.Nodes.First().Id;
        scene.View.Canvas.Navigation.Select(id.ToString());
        var json = scene.View.CopySelection();
        Assert.Empty(JsonNode.Parse(json)!["edges"]!.AsArray());
        Assert.Throws<InvalidOperationException>(() => scene.View.CutSelection(static _ => throw new InvalidOperationException()));
        Assert.Equal(4, scene.Graph.Editor.Document.Nodes.Count);
        string? clipboard = null;
        Assert.True(scene.View.CutSelection(text => clipboard = text).Succeeded);
        Assert.NotNull(clipboard);
        Assert.Equal(3, scene.Graph.Editor.Document.Nodes.Count);
        scene.View.Undo();
        Assert.Equal(4, scene.Graph.Editor.Document.Nodes.Count);
        Assert.Equal([id.ToString()], scene.Canvas.Navigation.SelectedKeys);
    }

    [Fact]
    public void UnavailableAndExtendedContentRejectsPasteWithoutMutation()
    {
        var scene = new SandboxScene();
        scene.View.SelectAll();
        var root = JsonNode.Parse(scene.View.CopySelection())!;
        root["nodes"]![0]!["type"] = StableId.New().ToString();
        var before = scene.View.Serialize();
        Assert.False(scene.View.Paste(root.ToJsonString(), default).Succeeded);
        Assert.Contains(scene.View.Diagnostics, static d => d.Id == "GVG1001");
        Assert.Equal(before, scene.View.Serialize());
        Assert.False(scene.View.Editor.CanUndo);
        Assert.False(scene.View.Paste("not JSON", default).Succeeded);
        root = JsonNode.Parse(scene.View.CopySelection())!;
        root["nodes"]![0]!["future-data"] = "preserve";
        Assert.False(scene.View.Paste(root.ToJsonString(), default).Succeeded);
        Assert.Equal(before, scene.View.Serialize());
        root = JsonNode.Parse(scene.View.CopySelection())!;
        root["vendor-root"] = "preserve";
        Assert.False(scene.View.Paste(root.ToJsonString(), default).Succeeded);
        Assert.Equal(before, scene.View.Serialize());
    }

    [Fact]
    public void DomainRemapperReceivesEveryCopiedNodeAndPortIdentity()
    {
        var scene = new SandboxScene();
        scene.View.SelectAll();
        var count = scene.View.Editor.Document.Nodes.Count + scene.View.Editor.Document.Nodes.Sum(static node => node.Ports.Count)
            + scene.View.Editor.Document.Edges.Count;
        var calls = 0;
        scene.View.RemapClipboardPayload = (node, map) =>
        {
            calls++;
            Assert.Equal(count, map.Count);
            Assert.NotEqual(node.Id, map[node.Id]);
            return node.Payload;
        };
        Assert.True(scene.View.DuplicateSelection(new GraphPoint(16, 16)).Succeeded);
        Assert.Equal(4, calls);
    }

    [Fact]
    public void ClipboardPreservesExplicitNullInsteadOfApplyingTheCreationDefault()
    {
        var scene = new SandboxScene();
        var node = scene.View.Editor.Document.Nodes.First();
        scene.View.Editor.Execute(new SetPayloadCommand(node.Id, null));
        scene.Canvas.Navigation.Select(node.Id.ToString());
        Assert.True(scene.View.DuplicateSelection(default).Succeeded);
        var copied = ElementId.Parse(Assert.Single(scene.Canvas.Navigation.SelectedKeys));
        Assert.Null(scene.View.Editor.Document.GetNode(copied).Payload);
    }

    [Fact]
    public void VariableDependenciesAndUnknownAnnotationsRejectAtomically()
    {
        var source = new SandboxScene();
        var variable = new GraphVariable(ElementId.New(), "Parameter", ArithmeticGraph.NumberType.Id, VariableScope.Input);
        Assert.True(source.View.Editor.Execute(new SetVariableCommand(variable)).Succeeded);
        source.View.SelectAll();
        var target = new SandboxScene();
        Assert.False(target.View.Paste(source.View.CopySelection(), default).Succeeded);
        Assert.Equal(4, target.View.Editor.Document.Nodes.Count);
        Assert.True(source.View.Paste(source.View.CopySelection(), default).Succeeded);
        var root = JsonNode.Parse(source.View.CopySelection())!;
        root["nodes"]![0]!["annotations"] = new JsonObject { [StableId.New().ToString()] = "unknown" };
        Assert.False(source.View.Paste(root.ToJsonString(), default).Succeeded);
        Assert.Contains(source.View.Diagnostics, static d => d.Id == "GVG1001");
    }
}

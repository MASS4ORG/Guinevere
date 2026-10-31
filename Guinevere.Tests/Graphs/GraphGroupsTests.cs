using System.Text.Json.Nodes;
using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;

namespace Guinevere.Tests.Graphs;

public sealed class GraphGroupsTests
{
    [Fact]
    public void NestedGroupsShareHistoryPreserveTopologyAndRejectCycles()
    {
        var scene = new GroupsScene();
        var view = scene.Sandbox.View;
        var document = view.Editor.Document;
        var outer = document.Groups.Single(static group => group.Parent is null);
        var inner = document.Groups.Single(static group => group.Parent is not null);
        var nodes = document.Nodes.Count;
        var edges = document.Edges.Count;
        Assert.False(view.ReparentGroup(outer.Id, inner.Id).Succeeded);
        Assert.Contains(view.Diagnostics, static diagnostic => diagnostic.Id == "MG0010");
        Assert.True(view.RenameGroup(inner.Id, "Parameters").Succeeded);
        Assert.Equal("Parameters", inner.Title);
        view.Undo();
        Assert.Equal("Inputs", inner.Title);
        view.Redo();
        Assert.Equal("Parameters", inner.Title);
        Assert.True(view.SetGroupCollapsed(inner.Id, true).Succeeded);
        view.Refresh();
        Assert.Single(view.Nodes, node => node.ParentKey == outer.Id.ToString());
        Assert.Equal(2, view.Elements.Count(static item => item.Role == GraphElementRole.Port && item.TypeName == "Group boundary port"));
        Assert.True(view.Ungroup(inner.Id).Succeeded);
        view.Refresh();
        Assert.Single(document.Groups);
        Assert.All(document.Nodes.Take(3), node => Assert.Equal(outer.Id, document.GroupOf(node.Id)));
        view.Undo();
        view.Refresh();
        Assert.Contains(inner.Id, document.CollapsedGroups);
        Assert.Equal(nodes, document.Nodes.Count);
        Assert.Equal(edges, document.Edges.Count);
        view.SetGroupCollapsed(inner.Id, false);
        view.Refresh();
        view.Canvas.Navigation.SetSelection(document.Nodes.Take(1).Select(static node => node.Id.ToString()));
        Assert.True(view.MoveSelectionToGroup(null).Succeeded);
        Assert.Null(document.GroupOf(document.Nodes.First().Id));
        view.Undo();
        Assert.Equal(inner.Id, document.GroupOf(document.Nodes.First().Id));
        Assert.True(view.ReparentGroup(inner.Id, null).Succeeded);
        Assert.Null(inner.Parent);
    }

    [Fact]
    public void CollapsedBoundaryLinksKeepRealPortsAndNestedCollapseSurvivesSeparateLayout()
    {
        var scene = new GroupsScene();
        var view = scene.Sandbox.View;
        var document = view.Editor.Document;
        var outer = document.Groups.Single(static group => group.Parent is null);
        var inner = document.Groups.Single(static group => group.Parent is not null);
        view.SetGroupCollapsed(inner.Id, true);
        view.SetGroupCollapsed(outer.Id, true);
        view.Refresh();
        Assert.Single(view.Nodes);
        Assert.Single(view.Elements, static item => item.Role == GraphElementRole.Group);
        var link = Assert.Single(view.Elements, static item => item.Role == GraphElementRole.Edge);
        var boundary = Assert.Single(view.Elements, static item => item.TypeName == "Group boundary port");
        Assert.Equal(outer.Id.ToString(), boundary.ParentKey);
        Assert.Equal(boundary.Key, link.SourceKey);
        Assert.True(document.TryGetPort(ElementId.Parse(boundary.Key), out _));
        var topology = view.Serialize(false);
        Assert.Null(JsonNode.Parse(topology)!["layout"]);
        Assert.Equal(2, JsonNode.Parse(topology)!["groups"]!.AsArray().Count);
        var loaded = GraphDocumentView.Load(topology, document.Registry, view.SerializeLayout()).View!;
        Assert.Equal(view.Serialize(), loaded.Serialize());
        Assert.False(loaded.Editor.CanUndo);
        Assert.Equal(document.CollapsedGroups.Order(), loaded.Editor.Document.CollapsedGroups.Order());
        Assert.Empty(GraphDocumentView.Load(topology, document.Registry).View!.Editor.Document.CollapsedGroups);
        loaded.SetGroupCollapsed(outer.Id, false);
        loaded.Refresh();
        Assert.Equal(2, loaded.Elements.Count(static item => item.Role == GraphElementRole.Group));
        Assert.Equal(2, loaded.Nodes.Count);
        var bad = JsonNode.Parse(view.SerializeLayout())!;
        bad["collapsed"] = new JsonArray(ElementId.New().ToString());
        Assert.Null(GraphDocumentView.Load(topology, document.Registry, bad.ToJsonString()).View);
        bad["collapsed"] = new JsonArray(outer.Id.ToString(), outer.Id.ToString());
        Assert.Null(GraphDocumentView.Load(topology, document.Registry, bad.ToJsonString()).View);
        Assert.NotNull(GraphDocumentView.Load(topology, document.Registry, "{\"positions\":[]}").View);
    }

    [Fact]
    public void MovingSelectedAncestorAndDescendantMovesEachNodeOnceAndUndoesInOneStep()
    {
        var scene = new GroupsScene();
        var view = scene.Sandbox.View;
        var document = view.Editor.Document;
        var outer = document.Groups.Single(static group => group.Parent is null);
        var inner = document.Groups.Single(static group => group.Parent is not null);
        var before = document.Nodes.ToDictionary(static node => node.Id, node => view.Position(node.Id));
        view.SetGroupCollapsed(outer.Id, true);
        view.Refresh();
        view.Canvas.Navigation.Select(outer.Id.ToString());
        view.MoveSelection(new(50, 30));
        foreach (var node in document.Nodes)
        {
            var expected = before[node.Id];
            if (document.GroupOf(node.Id) is not null)
            {
                expected = new(expected.X + 50, expected.Y + 30);
            }
            Assert.Equal(expected, view.Position(node.Id));
        }
        view.Undo();
        Assert.All(document.Nodes, node => Assert.Equal(before[node.Id], view.Position(node.Id)));
        view.SetGroupCollapsed(outer.Id, false);
        view.Refresh();
        view.Canvas.Navigation.SetSelection([outer.Id.ToString(), inner.Id.ToString()]);
        view.MoveSelection(new(10, 0));
        Assert.Equal(before[document.Nodes.First().Id].X + 10, view.Position(document.Nodes.First().Id).X);
    }

    [Fact]
    public void GroupClipboardRemapsNestedMembershipAndCollapseAsOneUndoableEdit()
    {
        var scene = new GroupsScene();
        var view = scene.Sandbox.View;
        var document = view.Editor.Document;
        var outer = document.Groups.Single(static group => group.Parent is null);
        view.SetGroupCollapsed(outer.Id, true);
        view.Refresh();
        view.Canvas.Navigation.Select(outer.Id.ToString());
        var copied = view.CopySelection();
        Assert.True(view.Paste(copied, new(50, 60)).Succeeded);
        Assert.Equal(7, document.Nodes.Count);
        Assert.Equal(4, document.Groups.Count);
        Assert.Equal(2, document.CollapsedGroups.Count);
        var pasted = document.Groups.Single(group => group.Parent is null && group.Id != outer.Id);
        Assert.Single(document.ChildGroups(pasted.Id));
        Assert.Equal(2, document.ChildGroups(pasted.Id).Single().Nodes.Count);
        view.Undo();
        Assert.Equal(4, document.Nodes.Count);
        Assert.Equal(2, document.Groups.Count);
        view.Redo();
        Assert.Equal(4, document.Groups.Count);
        view.Refresh();
        view.Canvas.Navigation.Select(document.Nodes.Single(node => document.GroupOf(node.Id) is null).Id.ToString());
        var nodeCopy = JsonNode.Parse(view.CopySelection())!;
        Assert.Empty(nodeCopy["groups"]!.AsArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void GroupFramesAndBoundaryPortsDrawAndRemainKeyboardAccessible(int style)
    {
        using var harness = new FrameHarness(1100, 800);
        var playground = new GraphPlayground();
        playground.SelectScenario(3);
        playground.SelectStyle(style);
        var view = playground.Groups.Sandbox.View;
        harness.Frame(playground.Draw);
        harness.Frame(playground.Draw);
        var group = view.Editor.Document.Groups.First();
        view.Canvas.Navigation.Select(group.Id.ToString());
        harness.Gui.Focus.RequestFocus("groups-canvas");
        harness.Frame(playground.Draw);
        GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Enter);
        harness.Frame(playground.Draw);
        Assert.Contains(group.Id, view.Editor.Document.CollapsedGroups);
        Assert.Equal(group.Id.ToString(), view.Canvas.Navigation.FocusedKey);
        GraphCanvasTests.Key(harness, playground.Draw, KeyboardKey.Enter);
        harness.Frame(playground.Draw);
        Assert.DoesNotContain(group.Id, view.Editor.Document.CollapsedGroups);
        var header = harness.Gui.RootNode!.FindChildById("playground-header")!;
        Assert.True(header.Rect.H <= 64);
        if (Environment.GetEnvironmentVariable("MASS4_GRAPH_CAPTURE_DIR") is { } directory)
        {
            Directory.CreateDirectory(directory);
            using var image = harness.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            using var file = File.Create(Path.Combine(directory, $"groups-{style}.png"));
            png.SaveTo(file);
        }
    }
}

using System.Text.Json.Nodes;
using Example02;
using Guinevere.Graphs;
using Guinevere.Tests.Mocks;
using MASS4.Graph;
using GraphPoint = Guinevere.Graphs.GraphPoint;

namespace Guinevere.Tests.Graphs;

public sealed class GraphGroupsInteractionTests
{
    [Fact]
    public void MainMenusCreateNestMoveCollapseAndUngroupRealFrames()
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new GraphPlayground();
        harness.Frame(scene.Draw);
        harness.Frame(scene.Draw);
        var view = scene.Sandbox.View;
        view.Canvas.Navigation.SetSelection(view.Nodes.Take(2).Select(static node => node.Key));
        GraphPlaygroundInput.Menu(harness, scene, 4, 0);
        var outer = Assert.Single(view.Editor.Document.Groups);
        Assert.Equal(2, outer.Nodes.Count);
        GraphPlaygroundInput.Submenu(harness, scene, 4, 1, 0);
        Assert.Equal(outer.Id.ToString(), view.Canvas.Navigation.FocusedKey);
        GraphPlaygroundInput.Submenu(harness, scene, 4, 1, 1);
        Assert.Contains(outer.Id, view.Editor.Document.CollapsedGroups);
        GraphPlaygroundInput.Submenu(harness, scene, 4, 1, 1);
        Assert.DoesNotContain(outer.Id, view.Editor.Document.CollapsedGroups);
        var member = outer.Nodes.First();
        view.Canvas.Navigation.Select(member.ToString());
        GraphPlaygroundInput.Submenu(harness, scene, 4, 1, 3);
        var inner = view.Editor.Document.ChildGroups(outer.Id).Single();
        Assert.Contains(member, inner.Nodes);
        var innerRow = view.Editor.Document.Groups.OrderBy(static group => group.Title).ToList().IndexOf(inner) + 1;
        GraphPlaygroundInput.Submenu(harness, scene, 4, innerRow, 4);
        Assert.Null(inner.Parent);
        view.Canvas.Navigation.Select(member.ToString());
        var outerRow = view.Editor.Document.Groups.OrderBy(static group => group.Title).ToList().IndexOf(outer) + 1;
        GraphPlaygroundInput.Submenu(harness, scene, 4, outerRow, 2);
        Assert.Equal(outer.Id, view.Editor.Document.GroupOf(member));
        GraphPlaygroundInput.Menu(harness, scene, 4, 3);
        Assert.Null(view.Editor.Document.GroupOf(member));
        GraphPlaygroundInput.Submenu(harness, scene, 4, innerRow, 5);
        Assert.Single(view.Editor.Document.Groups);
        GraphPlaygroundInput.Submenu(harness, scene, 3, 2, 1);
        Assert.Equal(GraphLinkRouting.Straight, view.Routing);
        Assert.Equal("Blueprint", scene.StyleName);
        GraphPlaygroundInput.Command(harness, scene, "add-node");
        GraphPlaygroundInput.Command(harness, scene, "undo");
        GraphPlaygroundInput.Command(harness, scene, "redo");
    }

    [Fact]
    public void InspectorDisclosureAndHeaderDragUseRealHistoryWithoutChangingConnections()
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new GraphPlayground();
        scene.SelectScenario(3);
        var view = scene.Groups.Sandbox.View;
        harness.Frame(scene.Draw);
        harness.Frame(scene.Draw);
        var group = view.Editor.Document.Groups.Single(static group => group.Parent is null);
        var edges = view.Editor.Document.Edges.Count;
        view.Canvas.Navigation.Select(group.Id.ToString());
        harness.Frame(scene.Draw);
        Activate(harness, scene, "rename-group");
        Activate(harness, scene, "collapse-group");
        Assert.Contains(group.Id, view.Editor.Document.CollapsedGroups);
        Activate(harness, scene, "collapse-group");
        Assert.DoesNotContain(group.Id, view.Editor.Document.CollapsedGroups);
        var frame = view.Elements.Single(item => item.Key == group.Id.ToString());
        var header = view.Canvas.WorldToScreen(new GraphPoint(frame.Bounds.X + 90, frame.Bounds.Y + 16));
        var member = view.Editor.Document.Nodes.First();
        var before = view.Position(member.Id);
        harness.Input.MoveTo(header);
        harness.Input.PressButton();
        harness.Frame(scene.Draw);
        harness.Input.MoveTo(header + new Vector2(40, 24));
        harness.Frame(scene.Draw);
        harness.Input.ReleaseButton();
        harness.Frame(scene.Draw);
        Assert.NotEqual(before, view.Position(member.Id));
        view.Undo();
        Assert.Equal(before, view.Position(member.Id));
        harness.Frame(scene.Draw);
        frame = view.Elements.Single(item => item.Key == group.Id.ToString());
        var toggle = view.Canvas.WorldToScreen(new GraphPoint(frame.Bounds.X + 20, frame.Bounds.Y + 16));
        harness.Click(scene.Draw, toggle);
        harness.Frame(scene.Draw);
        harness.Frame(scene.Draw);
        Assert.Contains(group.Id, view.Editor.Document.CollapsedGroups);
        Assert.Equal(edges, view.Editor.Document.Edges.Count);
        Activate(harness, scene, "ungroup");
        Assert.Single(view.Editor.Document.Groups);
        view.Undo();
        harness.Frame(scene.Draw);
        view.Canvas.Navigation.Select(group.Id.ToString());
        Assert.True(view.DeleteSelection().Succeeded);
        view.Undo();
        view.Refresh();
        view.Canvas.Navigation.Focus(group.Id.ToString());
        Assert.True(view.Canvas.Navigation.Delete());
    }

    [Fact]
    public void ExtendedGroupDataRoundTripsButCannotBePastedLossily()
    {
        var scene = new GroupsScene();
        var root = JsonNode.Parse(scene.Sandbox.View.Serialize())!;
        root["groups"]![0]!["vendor"] = "preserve";
        var loaded = GraphDocumentView.Load(root.ToJsonString(), scene.Sandbox.Graph.Editor.Document.Registry).View!;
        Assert.Equal("preserve", JsonNode.Parse(loaded.Serialize())!["groups"]![0]!["vendor"]!.GetValue<string>());
        var group = loaded.Editor.Document.Groups.Single(group => group.Extensions.Count > 0);
        loaded.Canvas.Navigation.Select(group.Id.ToString());
        var copied = loaded.CopySelection();
        Assert.False(scene.Sandbox.View.Paste(copied, default).Succeeded);
        Assert.Equal("GVG1001", Assert.Single(scene.Sandbox.View.Diagnostics).Id);
        Assert.True(loaded.GroupSelection("Invalid", ElementId.New()).IsEmpty);
        var empty = loaded.GroupSelection("Empty");
        loaded.Refresh();
        Assert.Contains(loaded.Elements, item => item.Key == empty.ToString());
    }

    [Fact]
    public void CopyingNestedFramePromotesItsRootAndCutRemovesTheWholeSubtreeAtomically()
    {
        var scene = new GroupsScene();
        var view = scene.Sandbox.View;
        var document = view.Editor.Document;
        var inner = document.Groups.Single(static group => group.Parent is not null);
        view.Canvas.Navigation.Select(inner.Id.ToString());
        var copied = view.CopySelection();
        var group = Assert.Single(JsonNode.Parse(copied)!["groups"]!.AsArray());
        Assert.Null(group!["parent"]);
        Assert.True(view.Paste(copied, new(10, 10)).Succeeded);
        Assert.Equal(3, document.Groups.Count);
        view.Undo();
        view.Refresh();
        var outer = document.Groups.Single(static group => group.Parent is null);
        view.Canvas.Navigation.Select(outer.Id.ToString());
        Assert.True(view.CutSelection(static _ => { }).Succeeded);
        Assert.Empty(document.Groups);
        Assert.Single(document.Nodes);
        view.Undo();
        Assert.Equal(2, document.Groups.Count);
        Assert.Equal(4, document.Nodes.Count);
        Assert.Equal([outer.Id.ToString()], view.Canvas.Navigation.SelectedKeys);
        view.Canvas.Navigation.ClearSelection();
        view.Canvas.Navigation.Focus(outer.Id.ToString());
        Assert.Equal(2, JsonNode.Parse(view.CopySelection())!["groups"]!.AsArray().Count);
        view.Canvas.Navigation.Focus(document.Edges.First().Id.ToString());
        Assert.True(view.CutSelection(static _ => { }).Succeeded);
        Assert.Equal(2, document.Edges.Count);
        view.Undo();
        Assert.Equal(3, document.Edges.Count);
    }

    [Fact]
    public async Task GroupAndCollapseEditsKeepThePausedFsmInstanceSnapshot()
    {
        using var harness = new FrameHarness(1100, 800);
        var scene = new GraphPlayground();
        scene.SelectScenario(2);
        scene.Fsm.AutoRun = false;
        harness.Frame(scene.Draw);
        await scene.Fsm.Simulation.Completion;
        harness.Frame(scene.Draw);
        var snapshot = scene.Fsm.Simulation.Snapshot;
        var view = scene.Fsm.View;
        view.Canvas.Navigation.Select(view.Nodes[1].Key);
        var group = view.GroupSelection("Observed state");
        harness.Frame(scene.Draw);
        view.SetGroupCollapsed(group, true);
        harness.Frame(scene.Draw);
        view.RenameGroup(group, "Renamed frame");
        harness.Frame(scene.Draw);
        view.Ungroup(group);
        harness.Frame(scene.Draw);
        Assert.Same(snapshot, scene.Fsm.Simulation.Snapshot);
    }

    [Fact]
    public void ContextMenuUsesTheSameQueuedCommandsAsTheMainMenu()
    {
        using var harness = new FrameHarness(1000, 700);
        var scene = new GraphPlayground();
        harness.Frame(scene.Draw);
        harness.Frame(scene.Draw);
        scene.Sandbox.View.SelectAll();
        scene.Sandbox.View.ContextRequested!(default);
        harness.Frame(scene.Draw);
        var bar = harness.Gui.RootNode!.FindChildById("sandbox-selection-menu")!;
        harness.Gui.Focus.RequestFocus(bar.ChildNodes[0].Id);
        harness.Frame(scene.Draw);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Enter);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Down);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Down);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Down);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Down);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Enter);
        harness.Frame(scene.Draw);
        Assert.Contains("mass4.graph", harness.Input.GetClipboardText(), StringComparison.Ordinal);
    }

    static void Activate(FrameHarness harness, GraphPlayground scene, string id)
    {
        harness.Gui.Focus.RequestFocus(id);
        harness.Frame(scene.Draw);
        GraphCanvasTests.Key(harness, scene.Draw, KeyboardKey.Enter);
        harness.Frame(scene.Draw);
    }
}

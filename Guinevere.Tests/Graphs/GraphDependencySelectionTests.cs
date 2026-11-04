using Example02;
using Guinevere.Tests.Mocks;
using MASS4.Graph;

namespace Guinevere.Tests.Graphs;

public sealed class GraphDependencySelectionTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SelectedOrFocusedLinkSeedsBothEndpoints(bool upstream, bool selected)
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var document = view.Editor.Document;
        var nodes = document.Nodes.ToArray();
        var edge = document.Edges.Single(edge => document.GetPort(edge.Source).NodeId == nodes[0].Id);
        var navigation = scene.Canvas.Navigation;
        if (selected)
        {
            navigation.Select(edge.Id.ToString());
        }
        else
        {
            navigation.Focus(edge.Id.ToString());
        }
        var before = view.Serialize();
        var history = view.Editor.NextUndo;

        if (upstream)
        {
            view.SelectUpstream();
        }
        else
        {
            view.SelectDownstream();
        }

        var expected = upstream ? nodes.Take(3) : nodes.Where((_, index) => index != 1);
        Assert.Equal(expected.Select(node => node.Id.ToString()).Order(), navigation.SelectedKeys.Order());
        Assert.Equal(before, view.Serialize());
        Assert.Same(history, view.Editor.NextUndo);
    }

    [Theory]
    [InlineData("Select upstream")]
    [InlineData("Select downstream")]
    public void FsmMenuTraversesRealStateCyclesWithoutEditing(string label)
    {
        using var harness = new FrameHarness();
        var scene = new FsmScene { AutoRun = false };
        var view = scene.View;
        var document = view.Editor.Document;
        var transition = document.Nodes.First(node => node.TypeId == MASS4.Graph.State.StateNodes.Transition.Id);
        view.Canvas.Navigation.Select(transition.Id.ToString());
        var before = view.Serialize();
        var history = view.Editor.NextUndo;

        scene.Commands(harness.Gui).Single(command => command.Label == label).Run();

        Assert.Contains(transition.Id.ToString(), view.Canvas.Navigation.SelectedKeys);
        Assert.True(view.Canvas.Navigation.SelectedKeys.Count > 1);
        Assert.All(view.Canvas.Navigation.SelectedKeys, key =>
            Assert.True(document.TryGetNode(ElementId.Parse(key), out _)));
        Assert.Equal(before, view.Serialize());
        Assert.Same(history, view.Editor.NextUndo);
    }

    [Fact]
    public void PlaygroundCommandsUseAllSelectedSeedsAndKeepFocus()
    {
        using var harness = new FrameHarness();
        var scene = new SandboxScene();
        var nodes = scene.View.Editor.Document.Nodes.ToArray();
        var navigation = scene.Canvas.Navigation;
        navigation.SetSelection(nodes.Take(2).Select(node => node.Id.ToString()), nodes[0].Id.ToString());

        scene.Commands(harness.Gui).Single(command => command.Label == "Select downstream").Run();

        Assert.Equal(4, navigation.SelectedKeys.Count);
        Assert.Equal(nodes[0].Id.ToString(), navigation.FocusedKey);
        navigation.Select(nodes[3].Id.ToString());
        scene.Commands(harness.Gui).Single(command => command.Label == "Select upstream").Run();
        Assert.Equal(4, navigation.SelectedKeys.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectionFollowsOnlyRequestedDirectionWithoutEditing(bool upstream)
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var nodes = view.Editor.Document.Nodes.ToArray();
        var isolated = view.AddNode(ArithmeticGraph.Number, new(900, 900));
        var history = view.Editor.NextUndo;
        var before = view.Serialize();
        var changes = 0;
        view.Editor.Document.Changed += (_, _) => changes++;
        view.Canvas.Navigation.Select(nodes[2].Id.ToString());

        if (upstream)
        {
            view.SelectUpstream();
        }
        else
        {
            view.SelectDownstream();
        }

        var expected = upstream ? nodes.Take(3) : nodes.Skip(2);
        Assert.Equal(expected.Select(node => node.Id.ToString()).Order(),
            view.Canvas.Navigation.SelectedKeys.Order());
        Assert.DoesNotContain(isolated.ToString(), view.Canvas.Navigation.SelectedKeys);
        Assert.Equal(before, view.Serialize());
        Assert.Same(history, view.Editor.NextUndo);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void FocusedPortSeedsItsOwnerAndNoFocusDoesNothing()
    {
        var scene = new SandboxScene();
        var view = scene.View;
        view.SelectUpstream();
        Assert.Empty(scene.Canvas.Navigation.SelectedKeys);
        var result = view.Editor.Document.Nodes.Last();
        scene.Canvas.Navigation.Focus(result.Ports[0].Id.ToString());
        view.SelectUpstream();
        Assert.Equal(4, scene.Canvas.Navigation.SelectedKeys.Count);
    }

    [Fact]
    public void CyclesTerminateAndSelectEachNodeOnce()
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var document = view.Editor.Document;
        var nodes = document.Nodes.ToArray();
        var permissive = new GraphEditor(document);
        // Close a cycle from Result back to Multiply's input after removing its original source.
        var input = nodes[2].Ports[0].Id;
        Assert.True(permissive.Execute(new DisconnectCommand(document.GetEdges(input).Single().Id)).Succeeded);
        Assert.True(permissive.Connect(nodes[3].Ports[1].Id, input).Succeeded);
        view.Refresh();
        scene.Canvas.Navigation.Select(nodes[2].Id.ToString());
        view.SelectDownstream();
        Assert.Equal(nodes.Skip(2).Select(node => node.Id.ToString()).Order(),
            scene.Canvas.Navigation.SelectedKeys.Order());
    }

    [Fact]
    public void TraversalCrossesCollapsedGroupsAndGroupsSeedTheirDescendants()
    {
        var scene = new SandboxScene();
        var view = scene.View;
        var nodes = view.Editor.Document.Nodes.ToArray();
        scene.Canvas.Navigation.SetSelection(nodes.Take(2).Select(node => node.Id.ToString()));
        var group = view.GroupSelection("Inputs");
        Assert.True(view.SetGroupCollapsed(group, true).Succeeded);
        scene.Canvas.Navigation.Select(nodes[3].Id.ToString());
        view.SelectUpstream();
        Assert.Equal(new[] { group, nodes[2].Id, nodes[3].Id }.Select(id => id.ToString()).Order(),
            scene.Canvas.Navigation.SelectedKeys.Order());

        scene.Canvas.Navigation.Select(group.ToString());
        view.SelectDownstream();
        Assert.Equal(3, scene.Canvas.Navigation.SelectedKeys.Count);
        Assert.Contains(nodes[3].Id.ToString(), scene.Canvas.Navigation.SelectedKeys);
        Assert.Contains(group, view.Editor.Document.CollapsedGroups);
    }
}

using Guinevere.Graphs;

namespace Guinevere.Tests.Graphs;

public sealed class GraphNavigationTests
{
    internal static GraphElementView[] Catalog() =>
    [
        new("a", GraphElementRole.Node, new GraphBounds(0, 0, 100, 80), "Source"),
        new("b", GraphElementRole.Node, new GraphBounds(1000, 0, 100, 80), "Target"),
        new("out", GraphElementRole.Port, new GraphBounds(95, 30, 10, 10), "Output", "a", "float"),
        new("in", GraphElementRole.Port, new GraphBounds(995, 30, 10, 10), "Input", "b", "float"),
        new("extra", GraphElementRole.Port, new GraphBounds(995, 50, 10, 10), "Other input", "b"),
        new("edge", GraphElementRole.Edge, new GraphBounds(100, 30, 900, 10), "Connection",
            SourceKey: "out", TargetKey: "in"),
        new("disabled", GraphElementRole.Node, new GraphBounds(200, 0, 100, 80), "Unavailable", Enabled: false)
    ];

    [Fact]
    public void SpatialFocusAndPortTraversalDoNotRequireLayoutControls()
    {
        var navigation = new GraphNavigation();
        navigation.Update(Catalog());
        Assert.False(navigation.Focus("missing"));
        Assert.False(navigation.Focus("disabled"));
        Assert.False(navigation.Move((FocusDirection)100));
        Assert.False(navigation.Move(FocusDirection.Next));
        Assert.True(navigation.Move(FocusDirection.Right));
        Assert.Equal("a", navigation.FocusedKey);
        Assert.True(navigation.Activate());
        Assert.Equal("out", navigation.FocusedKey);
        Assert.True(navigation.Move(FocusDirection.Right));
        Assert.Equal("b", navigation.FocusedKey);
        Assert.False(navigation.Move(FocusDirection.Right));
        Assert.True(navigation.MovePort(true));
        Assert.Equal("extra", navigation.FocusedKey);
        Assert.True(navigation.MovePort());
        Assert.Equal("in", navigation.FocusedKey);
        Assert.True(navigation.MovePort());
        Assert.Equal("extra", navigation.FocusedKey);
        Assert.True(navigation.Back());
        Assert.Equal("b", navigation.FocusedElement?.Key);
        Assert.False(navigation.Back());
        Assert.True(navigation.Move(FocusDirection.Left));
        Assert.Equal("a", navigation.FocusedKey);
        Assert.False(navigation.Move(FocusDirection.Up));
    }

    [Fact]
    public void SpatialNavigationUsesStableTiesAndDirectionalScoring()
    {
        var navigation = new GraphNavigation();
        navigation.Update([
            new("center", GraphElementRole.Node, new GraphBounds(0, 0, 10, 10), "Center"),
            new("z", GraphElementRole.Node, new GraphBounds(100, 100, 10, 10), "Z"),
            new("a", GraphElementRole.Node, new GraphBounds(100, -100, 10, 10), "A")
        ]);
        navigation.Focus("center");
        Assert.True(navigation.Move(FocusDirection.Right));
        Assert.Equal("a", navigation.FocusedKey);
        Assert.True(navigation.Move(FocusDirection.Down));
        Assert.Equal("z", navigation.FocusedKey);
        Assert.True(navigation.Move(FocusDirection.Up));
        Assert.Equal("a", navigation.FocusedKey);
    }

    [Fact]
    public void ConnectionsAndDeletionAreDelegatedAndValidationIsAnnounced()
    {
        var navigation = new GraphNavigation();
        navigation.Update(Catalog());
        var events = new List<GraphSemanticEvent>();
        navigation.Changed += events.Add;
        navigation.Focus("out");
        Assert.True(navigation.Activate());
        Assert.Equal("out", navigation.ConnectionSourceKey);
        navigation.Focus("in");
        Assert.True(navigation.Activate());
        Assert.Equal("out", navigation.ConnectionSourceKey);
        Assert.Equal(GraphSemanticEventKind.ValidationFailed, events[^1].Kind);
        GraphEditRequest? request = null;
        navigation.EditRequested = edit =>
        {
            request = edit;
            return new GraphEditResult(false, "MG0001: incompatible ports");
        };
        navigation.Activate();
        Assert.Equal(new GraphEditRequest(GraphEditKind.Connect, "out", "in"), request);
        Assert.Contains("MG0001", events[^1].Message);
        navigation.EditRequested = edit =>
        {
            request = edit;
            return new GraphEditResult(true, "Command completed.");
        };
        navigation.Activate();
        Assert.Null(navigation.ConnectionSourceKey);
        Assert.Equal(GraphSemanticEventKind.ConnectionCreated, events[^1].Kind);
        navigation.Focus("edge");
        Assert.True(navigation.Delete());
        Assert.Equal(new GraphEditRequest(GraphEditKind.Delete, "edge"), request);
        Assert.Equal(GraphSemanticEventKind.ElementDeleted, events[^1].Kind);
        Assert.False(navigation.Activate());
    }

    [Fact]
    public void SelectionAndConnectionCancellationRemainViewState()
    {
        var navigation = new GraphNavigation();
        var items = Catalog();
        navigation.Update(items);
        var events = new List<GraphSemanticEvent>();
        navigation.Changed += events.Add;
        Assert.False(navigation.ToggleSelection());
        Assert.False(navigation.Delete());
        Assert.False(navigation.MovePort());
        Assert.False(navigation.Back());
        Assert.True(navigation.Activate());
        Assert.True(navigation.ToggleSelection());
        Assert.Contains("a", navigation.SelectedKeys);
        Assert.True(navigation.ToggleSelection());
        Assert.Empty(navigation.SelectedKeys);
        navigation.Focus("out");
        navigation.Activate();
        Assert.True(navigation.Back());
        Assert.Null(navigation.ConnectionSourceKey);
        Assert.Equal(GraphSemanticEventKind.ConnectionCancelled, events[^1].Kind);
        navigation.Activate();
        navigation.ToggleSelection();
        var revision = navigation.FocusRevision;
        navigation.Update([]);
        Assert.True(navigation.FocusRevision > revision);
        Assert.Null(navigation.FocusedKey);
        Assert.Null(navigation.ConnectionSourceKey);
        Assert.Empty(navigation.SelectedKeys);
        Assert.Contains(events, static change => change.Kind == GraphSemanticEventKind.SelectionChanged);
        Assert.False(navigation.Activate());
        Assert.False(navigation.MovePort());
    }

    [Fact]
    public void EdgesCanBeInspectedCycledAndReturnedToAnAvailableEndpoint()
    {
        var navigation = new GraphNavigation();
        Assert.False(navigation.MoveEdge());
        navigation.Update(Catalog());
        Assert.True(navigation.MoveEdge(true));
        Assert.Equal("edge", navigation.FocusedKey);
        Assert.True(navigation.MoveEdge());
        Assert.True(navigation.MoveEdge(true));
        Assert.True(navigation.Back());
        Assert.Equal("out", navigation.FocusedKey);
        navigation.Update(Catalog().Select(static item =>
            item.Key == "edge" ? item with { SourceKey = "missing" } : item));
        navigation.MoveEdge();
        Assert.True(navigation.Back());
        Assert.Equal("in", navigation.FocusedKey);
        navigation.Update([Catalog()[5] with { SourceKey = null, TargetKey = null }]);
        navigation.MoveEdge();
        Assert.False(navigation.Back());
    }

    [Fact]
    public void PaletteHooksAndInvalidCatalogsDoNotCreateGraphModels()
    {
        var navigation = new GraphNavigation();
        navigation.Update(Catalog());
        var searches = new List<GraphSearchKind>();
        navigation.SearchRequested = searches.Add;
        navigation.Search(GraphSearchKind.Commands);
        navigation.Search(GraphSearchKind.CreateNode);
        Assert.Equal([GraphSearchKind.Commands, GraphSearchKind.CreateNode], searches);
        Assert.Throws<ArgumentException>(() => navigation.Update([Catalog()[0], Catalog()[0]]));
        Assert.Throws<ArgumentException>(() => navigation.Update([Catalog()[0] with { Key = "" }]));
        Assert.True(navigation.Focus("a"));
        var revision = navigation.FocusRevision;
        Assert.True(navigation.Focus("a"));
        Assert.Equal(revision, navigation.FocusRevision);
        navigation.Update([Catalog()[0]]);
        Assert.False(navigation.MovePort());
        Assert.False(navigation.Activate());
        navigation.Update([Catalog()[2]]);
        Assert.True(navigation.Focus("out"));
        Assert.False(navigation.MovePort());
        Assert.False(navigation.Back());
    }
}

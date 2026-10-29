using MASS4.Graph;

namespace Guinevere.Graphs;

/// <summary>The edge or center to align across selected node cards.</summary>
public enum GraphAlignment
{
    /// <summary>Left edges.</summary>
    Left,
    /// <summary>Horizontal centers.</summary>
    Center,
    /// <summary>Right edges.</summary>
    Right,
    /// <summary>Top edges.</summary>
    Top,
    /// <summary>Vertical centers.</summary>
    Middle,
    /// <summary>Bottom edges.</summary>
    Bottom
}

public sealed partial class GraphDocumentView
{
    /// <summary>Snaps completed node drags to <see cref="GridSize"/> in world coordinates.</summary>
    public bool SnapToGrid { get; set; }
    /// <summary>The positive world-unit interval used for snapping and keyboard nudges.</summary>
    public double GridSize { get; set; } = 16;

    /// <summary>Selects every node and link, excluding ports and the transient preview.</summary>
    public void SelectAll() => Canvas.Navigation.SetSelection(_views.Where(static item => item.Enabled
        && item.Role is GraphElementRole.Node or GraphElementRole.Edge).Select(static item => item.Key));

    /// <summary>Selects intersecting node cards and links crossing a world rectangle.</summary>
    public void SelectRectangle(GraphBounds bounds, bool additive = false, bool toggle = false)
    {
        var keys = _views.Where(item => item.Enabled && (item.Role == GraphElementRole.Node
            && item.Bounds.Intersects(bounds) || item.Role == GraphElementRole.Edge && LinkIntersects(item, bounds)))
            .Select(static item => item.Key).ToHashSet(StringComparer.Ordinal);
        var selected = Canvas.Navigation.SelectedKeys;
        Canvas.Navigation.SetSelection(toggle ? selected.Except(keys).Concat(keys.Except(selected))
            : additive ? selected.Concat(keys) : keys);
    }

    bool LinkIntersects(GraphElementView item, GraphBounds bounds)
    {
        var points = _connectors[item.Key].Polyline();
        for (var i = 1; i < points.Length; i++)
        {
            if (SegmentIntersects(points[i - 1], points[i], bounds))
            {
                return true;
            }
        }
        return false;
    }

    static bool SegmentIntersects(GraphPoint a, GraphPoint b, GraphBounds bounds)
    {
        var min = 0d;
        var max = 1d;
        return Clip(a.X, b.X - a.X, bounds.X, bounds.Right, ref min, ref max)
            && Clip(a.Y, b.Y - a.Y, bounds.Y, bounds.Bottom, ref min, ref max);
    }

    static bool Clip(double origin, double delta, double lower, double upper, ref double min, ref double max)
    {
        if (delta == 0)
        {
            return origin >= lower && origin <= upper;
        }
        var a = (lower - origin) / delta;
        var b = (upper - origin) / delta;
        min = Math.Max(min, Math.Min(a, b));
        max = Math.Min(max, Math.Max(a, b));
        return min <= max;
    }

    /// <summary>Deletes selected nodes and links atomically, or the focused element when selection is empty.</summary>
    public CommandResult DeleteSelection()
    {
        var steps = DeletionCommands(SelectionOrFocus());
        if (steps.Count == 0)
        {
            return Accept(new CommandResult(false, []));
        }
        if (Canvas.Navigation.ConnectionSourceKey is not null)
        {
            Canvas.Navigation.Back();
        }
        return ExecuteEdit(new CompositeCommand("Delete selection", steps), []);
    }

    IEnumerable<string> SelectionOrFocus() => Canvas.Navigation.SelectedKeys.Count > 0 ? Canvas.Navigation.SelectedKeys
        : Canvas.Navigation.FocusedKey is { } focus ? [focus] : [];

    List<IGraphCommand> DeletionCommands(IEnumerable<string> keys)
    {
        var ids = keys.Where(static key => ElementId.TryParse(key, out _)).Select(ElementId.Parse).ToHashSet();
        var nodes = Editor.Document.Nodes.Where(node => ids.Contains(node.Id)).ToArray();
        var ports = nodes.SelectMany(static node => node.Ports).Select(static port => port.Id).ToHashSet();
        var steps = Editor.Document.Edges.Where(edge => DisconnectsSelection(edge, ids, ports))
            .Select(static edge => (IGraphCommand)new DisconnectCommand(edge.Id)).ToList();
        steps.AddRange(nodes.Select(static node => (IGraphCommand)new RemoveNodeCommand(node.Id)));
        steps.AddRange(Editor.Document.Groups.Where(group => ids.Contains(group.Id))
            .Select(static group => (IGraphCommand)new RemoveGroupCommand(group.Id)));
        return steps;
    }

    static bool DisconnectsSelection(GraphEdge edge, HashSet<ElementId> selected, HashSet<ElementId> removedPorts) =>
        !removedPorts.Contains(edge.Source) && !removedPorts.Contains(edge.Target)
        && (selected.Contains(edge.Id) || selected.Contains(edge.Source) || selected.Contains(edge.Target));

    /// <summary>Aligns selected cards in a single reversible view command.</summary>
    public void AlignSelection(GraphAlignment alignment)
    {
        var nodes = SelectedNodes();
        if (nodes.Length < 2)
        {
            return;
        }
        var before = SnapshotLayout();
        var bounds = nodes.Select(node => _nodes.Single(view => view.Key == node.Id.ToString()).Bounds).ToArray();
        var horizontal = alignment is GraphAlignment.Left or GraphAlignment.Center or GraphAlignment.Right;
        var target = AlignmentCoordinate(bounds.Aggregate(static (a, b) => a.Union(b)), alignment);
        for (var i = 0; i < nodes.Length; i++)
        {
            var delta = target - AlignmentCoordinate(bounds[i], alignment);
            var p = Position(nodes[i].Id);
            SetPosition(nodes[i].Id, horizontal ? new GraphPoint(p.X + delta, p.Y) : new GraphPoint(p.X, p.Y + delta));
        }
        CommitLayout("Align nodes", before);
        Refresh();
    }

    static double AlignmentCoordinate(GraphBounds bounds, GraphAlignment alignment) => alignment switch
    {
        GraphAlignment.Left => bounds.X,
        GraphAlignment.Right => bounds.Right,
        GraphAlignment.Top => bounds.Y,
        GraphAlignment.Bottom => bounds.Bottom,
        GraphAlignment.Center => bounds.Center.X,
        _ => bounds.Center.Y
    };

    /// <summary>Distributes selected cards with equal gaps while retaining the outermost cards.</summary>
    public void DistributeSelection(bool horizontal)
    {
        var cards = _nodes.Where(view => Canvas.Navigation.SelectedKeys.Contains(view.Key))
            .OrderBy(view => horizontal ? view.Bounds.X : view.Bounds.Y).ThenBy(static view => view.Key, StringComparer.Ordinal)
            .ToArray();
        if (cards.Length < 3)
        {
            return;
        }
        var before = SnapshotLayout();
        var start = horizontal ? cards[0].Bounds.X : cards[0].Bounds.Y;
        var end = horizontal ? cards[^1].Bounds.Right : cards[^1].Bounds.Bottom;
        var sum = cards.Sum(view => horizontal ? view.Bounds.Width : view.Bounds.Height);
        var gap = (end - start - sum) / (cards.Length - 1);
        foreach (var card in cards)
        {
            var id = ElementId.Parse(card.Key);
            var position = Position(id);
            SetPosition(id, horizontal ? new GraphPoint(start, position.Y) : new GraphPoint(position.X, start));
            start += (horizontal ? card.Bounds.Width : card.Bounds.Height) + gap;
        }
        CommitLayout("Distribute nodes", before);
        Refresh();
    }

    /// <summary>Snaps selected positions to the configured world grid as one undoable operation.</summary>
    public void SnapSelection()
    {
        var before = SnapshotLayout();
        SnapSelectedPositions();
        CommitLayout("Snap nodes", before);
        Refresh();
    }

    void SnapSelectedPositions()
    {
        foreach (var node in SelectedNodes())
        {
            SetPosition(node.Id, Snapped(Position(node.Id)));
        }
    }

    GraphPoint Snapped(GraphPoint point)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(GridSize);
        GraphBounds.RequireFinite(GridSize);
        return new GraphPoint(Math.Round(point.X / GridSize) * GridSize, Math.Round(point.Y / GridSize) * GridSize);
    }

    GraphNode[] SelectedNodes() => [.. Editor.Document.Nodes.Where(node => Canvas.Navigation.SelectedKeys.Contains(node.Id.ToString()))];

    CommandResult ExecuteEdit(IGraphCommand command, string[] after) =>
        Accept(Editor.Execute(new SelectionCommand(this, command, [.. Canvas.Navigation.SelectedKeys], after)));

    sealed class SelectionCommand(GraphDocumentView view, IGraphCommand command, string[] before, string[] after)
        : IGraphCommand
    {
        readonly string? _focus = view.Canvas.Navigation.FocusedKey;
        /// <inheritdoc />
        public string Description => command.Description;
        /// <inheritdoc />
        public bool Apply(GraphDocument graph, DiagnosticBag diagnostics)
        {
            if (!command.Apply(graph, diagnostics))
            {
                return false;
            }
            view.Refresh();
            view.Canvas.Navigation.SetSelection(after, after.FirstOrDefault());
            return true;
        }
        /// <inheritdoc />
        public void Revert(GraphDocument graph)
        {
            command.Revert(graph);
            view.Refresh();
            view.Canvas.Navigation.SetSelection(before, _focus);
        }
    }
}

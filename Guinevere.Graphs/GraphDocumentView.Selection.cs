using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    /// <summary>Selects selected nodes and all their transitive input dependencies.</summary>
    /// <remarks>Uses focus when selection is empty. Groups seed their descendants; ports seed their owner;
    /// edges seed both endpoint nodes.
    /// Hidden nodes select their collapsed container. Does not change document content or undo history.</remarks>
    public void SelectUpstream() => SelectReachable(upstream: true);

    /// <summary>Selects selected nodes and all their transitive output consumers.</summary>
    /// <remarks>Uses the same seed and collapsed-group behavior as <see cref="SelectUpstream"/>.</remarks>
    public void SelectDownstream() => SelectReachable(upstream: false);

    void SelectReachable(bool upstream)
    {
        var document = Editor.Document;
        var seeds = SelectionOrFocus().Where(static key => ElementId.TryParse(key, out _))
            .Select(ElementId.Parse).ToHashSet();
        foreach (var id in seeds.ToArray())
        {
            if (document.TryGetPort(id, out var port))
            {
                seeds.Add(port.NodeId);
            }
            else if (document.TryGetEdge(id, out var edge))
            {
                seeds.Add(document.GetPort(edge.Source).NodeId);
                seeds.Add(document.GetPort(edge.Target).NodeId);
            }
        }
        var groups = seeds.Where(id => document.TryGetGroup(id, out _)).ToArray();
        var visited = document.Nodes.Where(node => seeds.Contains(node.Id)
            || groups.Any(id => InGroup(node.Id, id)))
            .Select(static node => node.Id).ToHashSet();
        if (visited.Count == 0)
        {
            return;
        }
        var neighbors = new Dictionary<ElementId, List<ElementId>>();
        foreach (var edge in document.Edges)
        {
            var source = document.GetPort(upstream ? edge.Target : edge.Source).NodeId;
            var target = document.GetPort(upstream ? edge.Source : edge.Target).NodeId;
            if (!neighbors.TryGetValue(source, out var targets))
            {
                neighbors.Add(source, targets = []);
            }
            targets.Add(target);
        }
        var pending = new Queue<ElementId>(visited);
        while (pending.TryDequeue(out var node))
        {
            if (!neighbors.TryGetValue(node, out var targets))
            {
                continue;
            }
            foreach (var target in targets.Where(visited.Add))
            {
                pending.Enqueue(target);
            }
        }
        Canvas.Navigation.SetSelection(visited.Select(id => (document.CollapsedContainer(id) ?? id).ToString()));
    }
}

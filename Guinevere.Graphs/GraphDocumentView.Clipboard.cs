using System.Text.Json.Nodes;
using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    /// <summary>Remaps domain-owned payload references when pasting, receiving node, port and edge identities.</summary>
    public Func<GraphNode, IReadOnlyDictionary<ElementId, ElementId>, object?>? RemapClipboardPayload { get; set; }

    /// <summary>Copies selected nodes and their internal links using the core's payload serializers and format.</summary>
    public string CopySelection()
    {
        var selected = Editor.Document.Nodes.Where(node => MovesWithSelection(node.Id))
            .Select(static node => node.Id).ToHashSet();
        var root = JsonNode.Parse(Serialize())!.AsObject();
        FilterClipboardNodes(root, selected);
        var ports = Editor.Document.Nodes.Where(node => selected.Contains(node.Id))
            .SelectMany(static node => node.Ports).Select(static port => port.Id).ToHashSet();
        FilterClipboardEdges(root, ports);
        FilterClipboardGroups(root, selected);
        root.Remove("guinevere");
        return root.ToJsonString();
    }

    static void FilterClipboardNodes(JsonObject root, HashSet<ElementId> selected)
    {
        var nodes = root["nodes"]!.AsArray();
        foreach (var node in nodes.ToArray())
        {
            if (!selected.Contains(ElementId.Parse(node!["id"]!.GetValue<string>())))
            {
                nodes.Remove(node);
            }
        }
    }

    static void FilterClipboardEdges(JsonObject root, HashSet<ElementId> ports)
    {
        var edges = root["edges"]!.AsArray();
        foreach (var edge in edges.ToArray())
        {
            if (!ports.Contains(ElementId.Parse(edge!["source"]!.GetValue<string>()))
                || !ports.Contains(ElementId.Parse(edge["target"]!.GetValue<string>())))
            {
                edges.Remove(edge);
            }
        }
    }

    /// <summary>Pastes a copied subgraph as one reversible command with fresh node, port and edge identities.</summary>
    public CommandResult Paste(string json, GraphPoint offset)
    {
        GraphBounds.RequireFinite(offset.X, offset.Y);
        var loaded = ReadDocument(json, Editor.Document.Registry);
        if (loaded.HasErrors || loaded.Document is not { } source)
        {
            return Accept(new CommandResult(false, loaded.Diagnostics));
        }
        if (source.Nodes.Count == 0 && source.Groups.Count == 0)
        {
            return Accept(new CommandResult(false, []));
        }
        if (ClipboardProblem(source) is { } problem)
        {
            return ClipboardError(problem);
        }
        var additions = source.Nodes.ToDictionary(static node => node.Id,
            node => new AddNodeCommand(source.Registry.NodeTypes.Resolve(node.TypeId), node.Payload)
            {
                Annotations = node.Annotations
            });
        var identities = ClipboardIdentities(source, additions);
        var steps = PasteCommands(source, additions, identities, offset);
        return ExecuteEdit(new CompositeCommand("Paste nodes", steps),
            [.. source.ChildGroups(null).Select(group => identities[group.Id].ToString()),
                .. source.Nodes.Where(node => source.GroupOf(node.Id) is null)
                    .Select(node => additions[node.Id].NodeId.ToString())]);
    }

    string? ClipboardProblem(GraphDocument source)
    {
        if (source.Extensions.Count > 0)
        {
            return "The selection contains document extensions that the insertion API cannot preserve.";
        }
        if (source.Nodes.Any(node => !ClipboardSchemaSupported(source, node) || UnsupportedClipboardData(node)))
        {
            return "This selection contains unavailable or extended node data; paste cannot preserve it through the published insertion API.";
        }
        if (source.Nodes.Any(node => !ClipboardPortsMatch(source, node)))
        {
            return "The selection contains preserved ports that the insertion API cannot recreate.";
        }
        if (source.Groups.Any(static group => group.Extensions.Count > 0))
        {
            return "The selection contains group extensions that the insertion API cannot preserve.";
        }
        return ClipboardVariablesProblem(source);
    }

    string? ClipboardVariablesProblem(GraphDocument source) =>
        source.Variables.Any(variable => !Editor.Document.TryGetVariable(variable.Id, out var existing)
            || existing != variable) ? "The selection references variables not present in this document." : null;

    static bool UnsupportedClipboardData(GraphNode node) => node.Payload is UnknownPayload
        || node.Extensions.Count > 0 || node.Annotations.Values.Any(static value => value is UnknownPayload);

    static bool ClipboardSchemaSupported(GraphDocument source, GraphNode node) =>
        source.TryGetNodeType(node, out var type) && type.Version == node.TypeVersion;

    static bool ClipboardPortsMatch(GraphDocument source, GraphNode node)
    {
        var type = source.Registry.NodeTypes.Resolve(node.TypeId);
        return node.Ports.Count == type.Ports.Count && node.Ports.All(port => type.TryGetPort(port.Declaration.Key, out _));
    }

    List<IGraphCommand> PasteCommands(GraphDocument source, Dictionary<ElementId, AddNodeCommand> additions,
        Dictionary<ElementId, ElementId> identities, GraphPoint offset)
    {
        var steps = new List<IGraphCommand>();
        foreach (var node in source.Nodes)
        {
            var add = additions[node.Id];
            steps.Add(add);
            var payload = RemapClipboardPayload is { } remap ? remap(node, identities) : node.Payload;
            steps.Add(new SetPayloadCommand(add.NodeId, payload));
            var p = source.Positions.GetValueOrDefault(node.Id);
            var position = new GraphPoint(p.X + offset.X, p.Y + offset.Y);
            if (_duplicateDrag && SnapToGrid)
            {
                position = Snapped(position);
            }
            steps.Add(new LayoutCommand(this, "Paste layout",
                [new LayoutEdit(add.NodeId, default, position)]));
        }
        steps.AddRange(source.Edges.Select(edge => new ConnectCommand(identities[edge.Source], identities[edge.Target], identities[edge.Id])));
        AddPasteGroups(source, identities, steps);
        return steps;
    }

    static Dictionary<ElementId, ElementId> ClipboardIdentities(GraphDocument source,
        Dictionary<ElementId, AddNodeCommand> additions)
    {
        var identities = new Dictionary<ElementId, ElementId>();
        foreach (var node in source.Nodes)
        {
            var add = additions[node.Id];
            identities.Add(node.Id, add.NodeId);
            foreach (var port in node.Ports)
            {
                identities.Add(port.Id, add.PortId(port.Declaration.Key));
            }
        }
        foreach (var edge in source.Edges)
        {
            identities.Add(edge.Id, ElementId.New());
        }
        foreach (var group in source.Groups)
        {
            identities.Add(group.Id, ElementId.New());
        }
        return identities;
    }

    /// <summary>Copies the selection and deletes it only after the host accepts its clipboard text.</summary>
    public CommandResult CutSelection(Action<string> writeClipboard)
    {
        ArgumentNullException.ThrowIfNull(writeClipboard);
        writeClipboard(CopySelection());
        var keys = SelectionOrFocus().Concat(ClipboardGroupIds().Select(static id => id.ToString()))
            .Concat(Editor.Document.Nodes.Where(node => MovesWithSelection(node.Id))
                .Select(static node => node.Id.ToString())).ToArray();
        var steps = DeletionCommands(keys);
        if (steps.Count == 0)
        {
            return Accept(new CommandResult(false, []));
        }
        if (Canvas.Navigation.ConnectionSourceKey is not null)
        {
            Canvas.Navigation.Back();
        }
        return ExecuteEdit(new CompositeCommand("Cut selection", steps), []);
    }

    /// <summary>Duplicates selected cards and their internal links with an optional world offset.</summary>
    public CommandResult DuplicateSelection(GraphPoint offset) => Paste(CopySelection(), offset);

    CommandResult ClipboardError(string message) => Accept(new CommandResult(false,
        [new Diagnostic("GVG1001", DiagnosticSeverity.Error, null, message)]));
}

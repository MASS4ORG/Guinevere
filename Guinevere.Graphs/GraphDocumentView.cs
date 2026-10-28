using MASS4.Graph;

namespace Guinevere.Graphs;

/// <summary>Adapts a real Graph editor to canvas descriptions while keeping layout and selection in the view.</summary>
public sealed partial class GraphDocumentView
{
    readonly Dictionary<ElementId, GraphPoint> _positions = [];
    readonly Dictionary<ElementId, GraphPoint> _initialPositions = [];
    readonly Dictionary<ElementId, GraphPoint> _ports = [];
    readonly Dictionary<string, GraphConnector> _connectors = new(StringComparer.Ordinal);
    readonly List<GraphElementView> _views = [];
    readonly List<GraphElementView> _nodes = [];
    const string PreviewKey = "connection-preview";
    GraphPoint _pointer;

    /// <summary>Creates an adapter without inspecting any domain payload.</summary>
    public GraphDocumentView(GraphEditor editor, GraphCanvas? canvas = null)
    {
        Editor = editor;
        Canvas = canvas ?? new GraphCanvas();
        Canvas.Navigation.EditRequested = Edit;
        Refresh();
    }

    /// <summary>The real reversible model editor.</summary>
    public GraphEditor Editor { get; }
    /// <summary>The virtualized canvas and its view-only navigation.</summary>
    public GraphCanvas Canvas { get; }
    /// <summary>The current node descriptions in document insertion order.</summary>
    public IReadOnlyList<GraphElementView> Nodes => _nodes;
    /// <summary>Current logical nodes, ports, and edges.</summary>
    public IReadOnlyList<GraphElementView> Elements => _views;
    /// <summary>Diagnostics from the last editor operation.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = [];
    /// <summary>Optional domain-owned presentation; the canvas never interprets payloads.</summary>
    public Func<GraphNode, string>? NodeTitle { get; set; }
    /// <summary>Rejects occupied-port and duplicate connections instead of replacing or retaining them.</summary>
    public bool AvoidReplacingConnections { get; set; }

    /// <summary>Searches the actual registry used by this document.</summary>
    public IEnumerable<NodeType> Palette(string query = "") => Editor.Document.Registry.NodeTypes.Entries
        .Where(type => type.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
        .OrderBy(static type => type.DisplayName, StringComparer.Ordinal);

    /// <summary>Adds a registered node through the editor and remembers its initial view position.</summary>
    public ElementId AddNode(NodeType type, GraphPoint position, object? payload = null)
    {
        GraphBounds.RequireFinite(position.X, position.Y);
        var command = new AddNodeCommand(type, payload);
        if (Accept(Editor.Execute(command)).Succeeded)
        {
            SetPosition(command.NodeId, position);
            Refresh();
            return command.NodeId;
        }
        return default;
    }

    /// <summary>Sets view metadata, retaining its initial value for layout reset.</summary>
    public void SetPosition(ElementId node, GraphPoint position)
    {
        GraphBounds.RequireFinite(position.X, position.Y);
        _positions[node] = position;
        _initialPositions.TryAdd(node, position);
    }

    /// <summary>Returns the current view position of an existing node.</summary>
    public GraphPoint Position(ElementId node) => _positions[node];

    /// <summary>Restores positions only, preserving nodes, names, ports, edges, and editor history.</summary>
    public void ResetLayout()
    {
        var before = SnapshotLayout();
        foreach (var node in Editor.Document.Nodes)
        {
            _positions[node.Id] = _initialPositions[node.Id];
        }
        CommitLayout("Reset layout", before);
        Refresh();
    }

    /// <summary>Connects two real ports through validation and rollback, accepting either click order.</summary>
    public CommandResult Connect(ElementId first, ElementId second)
    {
        var document = Editor.Document;
        var reverse = document.TryGetPort(first, out var port) && port.Direction == PortDirection.Input;
        return ConnectPorts(reverse ? second : first, reverse ? first : second);
    }

    /// <summary>Undoes the last document or layout command without replacing view identities.</summary>
    public CommandResult Undo() => Accept(Editor.Undo());
    /// <summary>Redoes the last document or layout command.</summary>
    public CommandResult Redo() => Accept(Editor.Redo());

    /// <summary>Rebuilds immutable view descriptions from the real document and current layout.</summary>
    public void Refresh()
    {
        _views.Clear();
        _nodes.Clear();
        _ports.Clear();
        _connectors.Clear();
        foreach (var node in Editor.Document.Nodes)
        {
            AddNodeViews(node);
        }
        var edges = new List<GraphElementView>();
        foreach (var edge in Editor.Document.Edges)
        {
            var connector = new GraphConnector(_ports[edge.Source], _ports[edge.Target], Routing);
            var key = edge.Id.ToString();
            _connectors.Add(key, connector);
            edges.Add(new GraphElementView(key, GraphElementRole.Edge, connector.Bounds, "Connection",
                SourceKey: edge.Source.ToString(), TargetKey: edge.Target.ToString()));
        }
        _views.InsertRange(0, edges);
        Canvas.Navigation.Update(_views);
    }

    void AddNodeViews(GraphNode node)
    {
        var position = EnsurePosition(node.Id);
        var (title, error) = DescribeNode(node);
        var view = new GraphElementView(node.Id.ToString(), GraphElementRole.Node,
            new GraphBounds(position.X, position.Y, NodeWidth, NodeHeight(node)), title,
            Error: error);
        _nodes.Add(view);
        _views.Add(view);
        var input = 0;
        var output = 0;
        foreach (var port in node.Ports)
        {
            var isInput = port.Direction == PortDirection.Input;
            var row = isInput ? input++ : output++;
            AddPortView(port, position, row);
        }
    }

    GraphPoint EnsurePosition(ElementId node)
    {
        if (_positions.TryGetValue(node, out var position))
        {
            return position;
        }
        var saved = Editor.Document.Positions.GetValueOrDefault(node);
        position = new GraphPoint(saved.X, saved.Y);
        SetPosition(node, position);
        return position;
    }

    (string Title, string? Error) DescribeNode(GraphNode node)
    {
        if (!Editor.Document.TryGetNodeType(node, out var type))
        {
            return ("Unknown node", "The node type is unavailable; its data is preserved.");
        }
        return (NodeTitle?.Invoke(node) ?? type.DisplayName, null);
    }

    void AddPortView(GraphPort port, GraphPoint position, int row)
    {
        var input = port.Direction == PortDirection.Input;
        var point = new GraphPoint(position.X + (input ? 0 : NodeWidth), position.Y + 54 + row * RowHeight);
        _ports.Add(port.Id, point);
        var typeName = Editor.Document.Registry.PortTypes.TryResolve(port.TypeId, out var portType)
            ? portType.DisplayName : "Unknown type";
        // Ports keep a usable on-screen target when zoomed out, without overlapping neighboring rows.
        var size = Math.Clamp(24 / Canvas.Camera.Zoom, 18, RowHeight);
        _views.Add(new GraphElementView(port.Id.ToString(), GraphElementRole.Port,
            new GraphBounds(point.X - size / 2, point.Y - size / 2, size, size), port.Declaration.Name,
            ParentKey: port.NodeId.ToString(), TypeName: typeName));
    }

    CommandResult Accept(CommandResult result)
    {
        Diagnostics = result.Diagnostics;
        return result;
    }

    GraphEditResult Edit(GraphEditRequest request)
    {
        if (!ElementId.TryParse(request.Key, out var key))
        {
            return new GraphEditResult(false, "Unknown graph element.");
        }
        CommandResult result;
        if (request.Kind == GraphEditKind.Connect)
        {
            if (!ElementId.TryParse(request.TargetKey, out var target))
            {
                return new GraphEditResult(false, "Unknown target port.");
            }
            result = Connect(key, target);
        }
        else
        {
            result = Delete(key);
        }
        var message = result.Diagnostics.Count > 0
            ? string.Join("; ", result.Diagnostics.Select(static diagnostic =>
                $"{diagnostic.Id}: {diagnostic.Message}"))
            : result.Succeeded ? "Graph command completed." : "No graph edit was available.";
        return new GraphEditResult(result.Succeeded, message);
    }

    CommandResult Delete(ElementId key)
    {
        if (Editor.Document.TryGetEdge(key, out _))
        {
            return Accept(Editor.Execute(new DisconnectCommand(key)));
        }
        if (Editor.Document.TryGetNode(key, out _))
        {
            return Accept(Editor.Execute(new RemoveNodeCommand(key)));
        }
        if (Editor.Document.TryGetPort(key, out _))
        {
            return DisconnectPort(key);
        }
        return new CommandResult(false, []);
    }
}

using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    readonly Dictionary<ElementId, GraphBounds> _groupBounds = [];
    ResolvedStyle _groupStyle = GraphStyles.Defaults("graph-group");
    ResolvedStyle _groupHeaderStyle = GraphStyles.Defaults("graph-group-header");
    readonly Queue<ElementId> _groupToggles = new();

    /// <summary>Groups selected nodes under an optional parent using one reversible model command.</summary>
    public ElementId GroupSelection(string title, ElementId? parent = null)
    {
        var nodes = SelectedNodes().Select(static node => node.Id).ToArray();
        var command = new AddGroupCommand(title, parent, nodes);
        return ExecuteEdit(command, [command.GroupId.ToString()]).Succeeded ? command.GroupId : default;
    }

    /// <summary>Renames an authored frame through the real editor.</summary>
    public CommandResult RenameGroup(ElementId group, string title) => Execute(new RenameGroupCommand(group, title));

    /// <summary>Removes a frame while preserving and promoting its nodes and nested frames.</summary>
    public CommandResult Ungroup(ElementId group) => Execute(new RemoveGroupCommand(group));

    /// <summary>Moves selected nodes into a group, or to the top level when null.</summary>
    public CommandResult MoveSelectionToGroup(ElementId? group) =>
        Execute(new SetNodeGroupCommand(SelectedNodes().Select(static node => node.Id).ToArray(), group));

    /// <summary>Reparents an authored frame; the model rejects missing parents and cycles.</summary>
    public CommandResult ReparentGroup(ElementId group, ElementId? parent) =>
        Execute(new SetGroupParentCommand(group, parent));

    /// <summary>Changes saved collapse state without altering membership, topology or evaluation.</summary>
    public CommandResult SetGroupCollapsed(ElementId group, bool collapsed)
    {
        var result = Execute(new SetGroupCollapsedCommand(group, collapsed));
        if (result.Succeeded)
        {
            Canvas.Navigation.Select(group.ToString());
        }
        return result;
    }

    void PrepareGroups()
    {
        _groupBounds.Clear();
        foreach (var group in Editor.Document.ChildGroups(null))
        {
            MeasureGroup(group);
        }
        foreach (var group in Editor.Document.ChildGroups(null))
        {
            AddGroupViews(group);
        }
    }

    GraphBounds MeasureGroup(GraphGroup group)
    {
        var content = MeasureGroupContent(group);
        var padding = GraphStyles.Length(_groupStyle, "padding-left");
        var header = GraphStyles.Length(_groupHeaderStyle, "height");
        var frame = new GraphBounds(content.X - padding, content.Y - padding - header,
            content.Width + padding * 2, content.Height + padding * 2 + header);
        if (Editor.Document.CollapsedGroups.Contains(group.Id))
        {
            var ports = BoundaryPorts(group.Id).ToArray();
            var rows = Math.Max(ports.Count(static port => port.Direction == PortDirection.Input),
                ports.Count(static port => port.Direction == PortDirection.Output));
            frame = new GraphBounds(frame.X, frame.Y, GraphStyles.Length(_groupStyle, "min-width"),
                header + Math.Max(1, rows) * GraphStyles.Length(_groupStyle, "gap") + padding);
        }
        _groupBounds[group.Id] = frame;
        return frame;
    }

    GraphBounds MeasureGroupContent(GraphGroup group)
    {
        GraphBounds? bounds = null;
        foreach (var id in group.Nodes)
        {
            var node = Editor.Document.GetNode(id);
            var position = EnsurePosition(id);
            var card = new GraphBounds(position.X, position.Y, NodeWidth(node), NodeHeight(node));
            bounds = bounds?.Union(card) ?? card;
        }
        foreach (var child in Editor.Document.ChildGroups(group.Id))
        {
            var childBounds = MeasureGroup(child);
            bounds = bounds?.Union(childBounds) ?? childBounds;
        }
        return bounds ?? new GraphBounds(40, 40, GraphStyles.Length(_groupStyle, "min-width"), 40);
    }

    void AddGroupViews(GraphGroup group)
    {
        var collapsed = Editor.Document.CollapsedGroups.Contains(group.Id);
        _views.Add(new GraphElementView(group.Id.ToString(), GraphElementRole.Group, _groupBounds[group.Id],
            group.Title, ParentKey: group.Parent?.ToString(), TypeName: "Group",
            StateDescription: $"{(collapsed ? "Collapsed" : "Expanded")}; {group.Nodes.Count} direct nodes; "
                + $"{Editor.Document.ChildGroups(group.Id).Count()} nested groups."));
        if (!collapsed)
        {
            foreach (var child in Editor.Document.ChildGroups(group.Id))
            {
                AddGroupViews(child);
            }
        }
    }

    bool InGroup(ElementId node, ElementId group) =>
        Editor.Document.GroupOf(node) is { } owner && Editor.Document.IsWithin(owner, group);

    IEnumerable<GraphPort> BoundaryPorts(ElementId group)
    {
        var document = Editor.Document;
        return document.Edges.Where(edge => InGroup(document.GetPort(edge.Source).NodeId, group)
                != InGroup(document.GetPort(edge.Target).NodeId, group))
            .Select(edge => document.GetPort(InGroup(document.GetPort(edge.Source).NodeId, group)
                ? edge.Source : edge.Target)).DistinctBy(static port => port.Id).OrderBy(static port => port.Id);
    }

    void AddBoundaryPorts()
    {
        foreach (var group in Editor.Document.Groups.Where(group =>
            Editor.Document.CollapsedGroups.Contains(group.Id) && Editor.Document.CollapsedContainer(group.Id) is null))
        {
            var bounds = _groupBounds[group.Id];
            var inputs = 0;
            var outputs = 0;
            foreach (var port in BoundaryPorts(group.Id))
            {
                var input = port.Direction == PortDirection.Input;
                var row = input ? inputs++ : outputs++;
                var point = new GraphPoint(input ? bounds.X : bounds.Right,
                    bounds.Y + GraphStyles.Length(_groupHeaderStyle, "height")
                    + (row + 0.5) * GraphStyles.Length(_groupStyle, "gap"));
                _ports[port.Id] = point;
                var size = Math.Min(PortHitRadius * 2 / Canvas.Camera.Zoom, GraphStyles.Length(_groupStyle, "gap"));
                _views.Add(new GraphElementView(port.Id.ToString(), GraphElementRole.Port,
                    new GraphBounds(point.X - size / 2, point.Y - size / 2, size, size), EndpointName(port.Id),
                    ParentKey: group.Id.ToString(), TypeName: "Group boundary port", StateDescription: PortState(port)));
            }
        }
    }

    ElementId? CollapsedEndpoint(ElementId port) =>
        Editor.Document.CollapsedContainer(Editor.Document.GetPort(port).NodeId);

    bool InternalCollapsedEdge(GraphEdge edge) =>
        CollapsedEndpoint(edge.Source) is { } source && source == CollapsedEndpoint(edge.Target);

    bool HasCollapsedEndpoint(GraphEdge edge) =>
        CollapsedEndpoint(edge.Source) is not null || CollapsedEndpoint(edge.Target) is not null;

    bool MovesWithSelection(ElementId node)
    {
        var keys = SelectionOrFocus();
        return keys.Any(key => key == node.ToString()
            || ElementId.TryParse(key, out var id) && Editor.Document.TryGetGroup(id, out _) && InGroup(node, id));
    }

    void DrawGroup(Gui gui, GraphElementView element)
    {
        gui.CurrentNode.HitTestVisible(false);
        var id = ElementId.Parse(element.Key);
        var collapsed = Editor.Document.CollapsedGroups.Contains(id);
        var modifiers = new List<string>();
        if (collapsed)
        {
            modifiers.Add("collapsed");
        }
        if (Canvas.Navigation.SelectedKeys.Contains(element.Key))
        {
            modifiers.Add("selected");
        }
        using var frame = gui.StyledNode("graph-group", modifiers: modifiers).Expand().Padding(0)
            .HitTestVisible(false).Enter();
        using var header = gui.StyledNode("graph-group-header").ExpandWidth()
            .Height(GraphStyles.Length(_groupHeaderStyle, "height")).Absolute(0, 0).HitTestVisible(true)
            .Direction(Axis.Horizontal).ContentAlignY(0.5f).Enter();
        gui.CurrentNode.On<PointerDownEvent>(e =>
        {
            if (e.Button == MouseButton.Left)
            {
                Canvas.Navigation.Select(element.Key, ControlHeld(gui));
                RequestCanvasFocus(gui, _canvasId);
                e.StopPropagation();
            }
        });
        gui.CurrentNode.On<DragStartEvent>(e => StartNodeDrag(gui, e));
        gui.CurrentNode.On<DragMoveEvent>(DragNodes);
        gui.CurrentNode.On<DragEndEvent>(EndNodeDrag);
        DrawGroupDisclosure(gui, element, id, collapsed);
        gui.DrawText(element.Name).HitTestVisible(false);
    }

    void DrawGroupDisclosure(Gui gui, GraphElementView element, ElementId id, bool collapsed)
    {
        using (gui.Node(22, GraphStyles.Length(_groupHeaderStyle, "height")).Enter())
        {
            gui.CurrentNode.On<PointerDownEvent>(static e => e.StopPropagation());
            var toggle = gui.Pressable(new ControlBehaviorOptions(Role: ControlRole.Button,
                Label: (collapsed ? "Expand " : "Collapse ") + element.Name));
            if (toggle.Activated)
            {
                _groupToggles.Enqueue(id);
                gui.RequestFrame();
            }
            if (gui.Pass == Pass.Pass2Render)
            {
                var center = gui.CurrentNode.Rect.Center;
                var a = collapsed ? new Vector2(-3, -5) : new Vector2(-5, -3);
                var b = collapsed ? new Vector2(-3, 5) : new Vector2(5, -3);
                var c = collapsed ? new Vector2(4, 0) : new Vector2(0, 4);
                gui.DrawTriangleFilled(center + a, center + b, center + c,
                    gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value);
            }
        }
    }
}

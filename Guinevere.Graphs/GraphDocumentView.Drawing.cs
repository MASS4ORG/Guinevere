using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    bool _togglePress;
    string _canvasId = "graph-document";

    /// <summary>Returns a real port's current view attachment point.</summary>
    public GraphPoint PortPosition(ElementId port) => _ports[port];

    /// <summary>Returns the curve drawn for an existing edge.</summary>
    public GraphConnector Connector(ElementId edge) => _connectors[edge.ToString()];

    /// <summary>Finds a port's attachment point as of the last refresh.</summary>
    public bool TryGetPortPosition(ElementId port, out GraphPoint position) => _ports.TryGetValue(port, out position);

    /// <summary>Finds an edge's routed connector as of the last refresh.</summary>
    public bool TryGetConnector(ElementId edge, out GraphConnector connector) =>
        _connectors.TryGetValue(edge.ToString(), out connector);

    /// <summary>Moves selected nodes, or the focused node, without mutating semantic graph state.</summary>
    public void MoveSelection(GraphPoint delta)
    {
        GraphBounds.RequireFinite(delta.X, delta.Y);
        var before = _dragStart is null ? SnapshotLayout() : null;
        foreach (var node in Editor.Document.Nodes)
        {
            if (MovesWithSelection(node.Id))
            {
                var position = Position(node.Id);
                SetPosition(node.Id, new GraphPoint(position.X + delta.X, position.Y + delta.Y));
            }
        }
        if (before is not null)
        {
            CommitLayout("Move nodes", before);
        }
    }

    /// <summary>Draws real nodes, labeled ports, connectors, and a transient link preview.</summary>
    public LayoutNode Draw(Gui gui, string id = "graph-document")
    {
        _canvasId = id;
        GraphStyles.Ensure(gui);
        using var editor = gui.StyledNode("graph-editor", classes: StyleClasses).Expand().Enter();
        if (gui.Pass == Pass.Pass1Build)
        {
            while (_groupToggles.TryDequeue(out var group))
            {
                SetGroupCollapsed(group, !Editor.Document.CollapsedGroups.Contains(group));
            }
            ResolveGeometry(gui);
            _pointer = Canvas.ScreenToWorld(gui.Input.MousePosition);
            Refresh();
            AddPreview();
        }
        var node = Canvas.Draw(gui, _views, DrawElement, id: id);
        node.On<PointerDownEvent>(e => SelectConnection(gui, node, e));
        AttachEditingGestures(gui, node);
        node.On<KeyDownEvent>(e => HandleEditorKey(gui, node, e), capture: true);
        return node;
    }

    void AddPreview()
    {
        if (ElementId.TryParse(Canvas.Navigation.ConnectionSourceKey, out var source)
            && _ports.TryGetValue(source, out var position))
        {
            var connector = new GraphConnector(position, _pointer, Routing);
            _connectors.Add(PreviewKey, connector);
            _views.Add(new GraphElementView(PreviewKey, GraphElementRole.Edge, connector.Bounds,
                "Connection preview", Enabled: false));
        }
    }

    void DrawElement(Gui gui, GraphElementView element)
    {
        switch (element.Role)
        {
            case GraphElementRole.Edge:
                gui.CurrentNode.HitTestVisible(false);
                DrawLink(gui, element);
                break;
            case GraphElementRole.Node:
                DrawNode(gui, element);
                break;
            case GraphElementRole.Port:
                DrawPort(gui, element);
                break;
            case GraphElementRole.Group:
                DrawGroup(gui, element);
                break;
        }
    }

    void DrawNode(Gui gui, GraphElementView element)
    {
        var node = gui.CurrentNode;
        node.Cursor(PointerCursor.Move).On<PointerDownEvent>(e =>
        {
            if (e.Button == MouseButton.Right && !Canvas.Navigation.SelectedKeys.Contains(element.Key))
            {
                Canvas.Navigation.Select(element.Key);
            }
            if (e.Button != MouseButton.Left)
            {
                return;
            }
            _togglePress = ControlHeld(gui) || ShiftHeld(gui);
            if (ShiftHeld(gui))
            {
                Canvas.Navigation.SetSelection(Canvas.Navigation.SelectedKeys.Append(element.Key), element.Key);
            }
            else if (_togglePress || !Canvas.Navigation.SelectedKeys.Contains(element.Key))
            {
                Canvas.Navigation.Select(element.Key, _togglePress);
            }
            RequestCanvasFocus(gui, node.Parent!.Id);
            e.StopPropagation();
        });
        node.On<ClickEvent>(e =>
        {
            if (e.Button == MouseButton.Left && !_togglePress)
            {
                Canvas.Navigation.Select(element.Key);
            }
        });
        node.On<DragStartEvent>(e => StartNodeDrag(gui, e));
        node.On<DragMoveEvent>(DragNodes);
        node.On<DragEndEvent>(EndNodeDrag);
        DrawNodeCard(gui, element);
    }

    void DrawPort(Gui gui, GraphElementView element)
    {
        var node = gui.CurrentNode;
        if (Canvas.Detail == GraphLevelOfDetail.Outline)
        {
            node.HitTestVisible(false);
            return;
        }
        var port = Editor.Document.GetPort(ElementId.Parse(element.Key));
        node.Cursor(PointerCursor.Crosshair).On<PointerDownEvent>(e =>
        {
            if (e.Button == MouseButton.Left)
            {
                Canvas.Navigation.Focus(element.Key);
                Canvas.Navigation.Activate();
                RequestCanvasFocus(gui, node.Parent!.Id);
                e.PreventDefault();
                e.StopPropagation();
            }
        });
        node.On<DragStartEvent>(static e => e.StopPropagation());
        node.On<DragMoveEvent>(static e => e.StopPropagation());
        node.On<DragEndEvent>(EndConnection);
        DrawPortMarker(gui, element, port);
    }

    void DragNodes(DragEvent e)
    {
        if (e.Button == MouseButton.Left && !_cancelledDrag)
        {
            var current = Canvas.ScreenToWorld(e.Position);
            var previous = Canvas.ScreenToWorld(e.Position - e.FrameDelta);
            MoveSelection(new GraphPoint(current.X - previous.X, current.Y - previous.Y));
            e.StopPropagation();
        }
    }

    void EndConnection(DragEvent e)
    {
        if (e.Button != MouseButton.Left || Canvas.Navigation.ConnectionSourceKey is null)
        {
            return;
        }
        var target = HitPort(Canvas.ScreenToWorld(e.Position), PortHitRadius / Canvas.Camera.Zoom);
        if (target is not null)
        {
            Canvas.Navigation.Focus(target);
            Canvas.Navigation.Activate();
        }
        else
        {
            Canvas.Navigation.Back();
        }
        e.StopPropagation();
    }

    /// <summary>Returns the visible port nearest a world point within a world radius.</summary>
    public string? HitPort(GraphPoint point, double radius)
    {
        string? best = null;
        var distance = radius * radius;
        foreach (var item in Canvas.VisibleElements)
        {
            if (item.Role == GraphElementRole.Port && _ports.TryGetValue(ElementId.Parse(item.Key), out var port)
                && SquaredDistance(point, port) <= distance)
            {
                best = item.Key;
                distance = SquaredDistance(point, port);
            }
        }
        return best;
    }

    void SelectConnection(Gui gui, LayoutNode canvas, PointerDownEvent e)
    {
        if (e.Button != MouseButton.Left || e.TargetId != canvas.Id)
        {
            return;
        }
        if (Canvas.Navigation.ConnectionSourceKey is not null)
        {
            Canvas.Navigation.Back();
            return;
        }
        var point = Canvas.ScreenToWorld(e.Position);
        var edge = HitLink(point, LinkHitTolerance / Canvas.Camera.Zoom);
        if (edge is not null)
        {
            SelectLink(gui, edge);
        }
        else if (!ControlHeld(gui) && !ShiftHeld(gui))
        {
            Canvas.Navigation.ClearSelection();
        }
    }

    void SelectLink(Gui gui, string edge)
    {
        if (ShiftHeld(gui))
        {
            Canvas.Navigation.SetSelection(Canvas.Navigation.SelectedKeys.Append(edge), edge);
        }
        else
        {
            Canvas.Navigation.Select(edge, ControlHeld(gui));
        }
    }

    /// <summary>Returns the visible link nearest a world point within a world tolerance, ignoring stroke width.</summary>
    public string? HitLink(GraphPoint point, double tolerance)
    {
        string? best = null;
        var distance = tolerance;
        foreach (var item in Canvas.VisibleElements)
        {
            if (item.Role == GraphElementRole.Edge && item.Key != PreviewKey && Near(item.Bounds, point, tolerance)
                && _connectors.TryGetValue(item.Key, out var connector) && connector.Distance(point) <= distance)
            {
                best = item.Key;
                distance = connector.Distance(point);
            }
        }
        return best;
    }

    static bool Near(GraphBounds bounds, GraphPoint point, double tolerance) =>
        point.X >= bounds.X - tolerance && point.X <= bounds.Right + tolerance
        && point.Y >= bounds.Y - tolerance && point.Y <= bounds.Bottom + tolerance;

    void HandleEditorKey(Gui gui, LayoutNode canvas, KeyDownEvent e)
    {
        if (e.TargetId != canvas.Id)
        {
            return;
        }
        if (!HandleEditingKey(gui, e.Key))
        {
            return;
        }
        e.PreventDefault();
        e.StopPropagation();
    }

    static void RequestCanvasFocus(Gui gui, string canvasId) =>
        gui.Focus.RequestFocus(canvasId, FocusReason.Mouse);

    static bool ControlHeld(Gui gui) => gui.Input.IsKeyDown(KeyboardKey.LeftControl)
        || gui.Input.IsKeyDown(KeyboardKey.RightControl);

    static bool ShiftHeld(Gui gui) => gui.Input.IsKeyDown(KeyboardKey.LeftShift)
        || gui.Input.IsKeyDown(KeyboardKey.RightShift);

    static double SquaredDistance(GraphPoint a, GraphPoint b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}

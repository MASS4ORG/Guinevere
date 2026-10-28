using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    bool _togglePress;

    /// <summary>Returns a real port's current view attachment point.</summary>
    public GraphPoint PortPosition(ElementId port) => _ports[port];

    /// <summary>Returns the curve drawn for an existing edge.</summary>
    public GraphConnector Connector(ElementId edge) => _connectors[edge.ToString()];

    /// <summary>Moves selected nodes, or the focused node, without mutating semantic graph state.</summary>
    public void MoveSelection(GraphPoint delta)
    {
        GraphBounds.RequireFinite(delta.X, delta.Y);
        var before = _dragStart is null ? SnapshotLayout() : null;
        foreach (var node in Editor.Document.Nodes)
        {
            var selected = Canvas.Navigation.SelectedKeys;
            var move = selected.Count > 0 ? selected.Contains(node.Id.ToString())
                : Canvas.Navigation.FocusedKey == node.Id.ToString();
            if (move)
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
        if (gui.Pass == Pass.Pass1Build)
        {
            _pointer = Canvas.ScreenToWorld(gui.Input.MousePosition);
            Refresh();
            AddPreview();
        }
        var node = Canvas.Draw(gui, _views, DrawElement, id: id);
        node.On<PointerDownEvent>(e => SelectConnection(node, e));
        node.On<KeyDownEvent>(e => HandleEditorKey(gui, node, e), capture: true);
        return node;
    }

    void AddPreview()
    {
        if (ElementId.TryParse(Canvas.Navigation.ConnectionSourceKey, out var source)
            && _ports.TryGetValue(source, out var position))
        {
            var connector = new GraphConnector(position, _pointer);
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
                var selected = Canvas.Navigation.SelectedKeys.Contains(element.Key)
                    || Canvas.Navigation.FocusedKey == element.Key;
                _connectors[element.Key].Draw(gui, selected ? Color.Gold : Color.LightSkyBlue);
                break;
            case GraphElementRole.Node:
                DrawNode(gui, element);
                break;
            case GraphElementRole.Port:
                DrawPort(gui, element);
                break;
        }
    }

    void DrawNode(Gui gui, GraphElementView element)
    {
        var node = gui.CurrentNode;
        node.Cursor(PointerCursor.Move).On<PointerDownEvent>(e =>
        {
            if (e.Button != MouseButton.Left)
            {
                return;
            }
            _togglePress = ControlHeld(gui);
            if (_togglePress || !Canvas.Navigation.SelectedKeys.Contains(element.Key))
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
        node.On<DragStartEvent>(BeginNodeDrag);
        node.On<DragMoveEvent>(DragNodes);
        node.On<DragEndEvent>(EndNodeDrag);
        var selected = Canvas.Navigation.SelectedKeys.Contains(element.Key);
        gui.DrawRectFilled(node.Rect, selected ? new Color(0x315c83ff) : new Color(0x29384eff), 10);
        using var title = gui.Node(194, 26).Absolute(13, 10).Enter();
        gui.DrawText(element.Name, 16, Color.White);
    }

    void DrawPort(Gui gui, GraphElementView element)
    {
        var port = Editor.Document.GetPort(ElementId.Parse(element.Key));
        var input = port.Direction == PortDirection.Input;
        var node = gui.CurrentNode;
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
        gui.DrawCircleFilled(node.Rect.Center, 7, input ? Color.LightSkyBlue : Color.MediumAquamarine);
        using var label = gui.Node(185, 20).Absolute(input ? 24 : -191, -2)
            .ContentAlignX(input ? 0 : 1).Enter();
        gui.DrawText(element.Name, 13, Color.LightSteelBlue);
    }

    void DragNodes(DragEvent e)
    {
        if (e.Button == MouseButton.Left)
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
        var point = Canvas.ScreenToWorld(e.Position);
        var nearest = _ports.MinBy(port => SquaredDistance(point, port.Value));
        if (SquaredDistance(point, nearest.Value) <= Math.Pow(18 / Canvas.Camera.Zoom, 2))
        {
            Canvas.Navigation.Focus(nearest.Key.ToString());
            Canvas.Navigation.Activate();
        }
        else
        {
            Canvas.Navigation.Back();
        }
        e.StopPropagation();
    }

    void SelectConnection(LayoutNode canvas, PointerDownEvent e)
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
        var closest = Editor.Document.Edges.MinBy(edge => _connectors[edge.Id.ToString()].Distance(point));
        if (closest is not null && _connectors[closest.Id.ToString()].Distance(point) <= 9 / Canvas.Camera.Zoom)
        {
            Canvas.Navigation.Select(closest.Id.ToString());
        }
        else
        {
            Canvas.Navigation.ClearSelection();
        }
    }

    void HandleEditorKey(Gui gui, LayoutNode canvas, KeyDownEvent e)
    {
        if (e.TargetId != canvas.Id || !ControlHeld(gui))
        {
            return;
        }
        if (e.Key == KeyboardKey.Z)
        {
            Undo();
        }
        else if (e.Key == KeyboardKey.Y)
        {
            Redo();
        }
        else
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

    static double SquaredDistance(GraphPoint a, GraphPoint b) =>
        (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}

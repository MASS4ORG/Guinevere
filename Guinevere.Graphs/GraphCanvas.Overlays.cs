namespace Guinevere.Graphs;

public sealed partial class GraphCanvas
{
    GraphMinimap? _minimapMapping;

    void DrawOverlays(Gui gui)
    {
        if (Overlays.Count == 0 || _frameSize.X <= 0 || _frameSize.Y <= 0)
        {
            return;
        }
        var node = gui.Node(_frameSize.X, _frameSize.Y).Absolute(0, 0).HitTestVisible(false);
        using var scope = node.Enter();
        var context = new GraphOverlayContext(this, gui.Pass, node.Rect.Position, _frameCamera.Zoom,
            _frameCamera.VisibleBounds(_frameSize), Detail);
        foreach (var overlay in Overlays)
        {
            overlay.Draw(gui, context);
        }
    }

    void DrawMinimap(Gui gui)
    {
        var width = Math.Min(180, _frameSize.X - 16);
        var height = Math.Min(120, _frameSize.Y - 16);
        if (!ShowMinimap || width <= 0 || height <= 0)
        {
            return;
        }
        var node = gui.Node(width, height).Absolute(_frameSize.X - width - 8, _frameSize.Y - height - 8)
            .BlockInput();
        using var scope = node.Enter();
        gui.SetClipped(true);
        node.On<PointerDownEvent>(e =>
        {
            if (e.Button == MouseButton.Left && _minimapMapping is { } mapping)
            {
                Camera.CenterOn(mapping.Unmap(node.ScreenToLocal(e.Position)), _viewportSize);
                e.PreventDefault();
                e.StopPropagation();
            }
        });
        node.On<DragStartEvent>(e => MoveMinimap(node, e));
        node.On<DragMoveEvent>(e => MoveMinimap(node, e));
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }
        var visible = _frameCamera.VisibleBounds(_frameSize);
        var mapping = new GraphMinimap(_contentBounds ?? visible, visible, node.Rect);
        _minimapMapping = mapping;
        gui.DrawRectFilled(node.Rect, Style.MinimapBackground);
        foreach (var item in _items)
        {
            if (item.Role == GraphElementRole.Node)
            {
                gui.DrawRectFilled(mapping.Map(item.Bounds), Style.MinimapNode);
            }
        }
        gui.DrawRect(mapping.Viewport).Stroke(Style.MinimapViewport, 1);
    }

    void MoveMinimap(LayoutNode node, DragEvent e)
    {
        if (e.Button == MouseButton.Left && _minimapMapping is { } mapping)
        {
            Camera.CenterOn(mapping.Unmap(node.ScreenToLocal(e.Position)), _viewportSize);
            e.StopPropagation();
        }
    }

    void PublishSemantics(Gui gui, LayoutNode canvas)
    {
        var sink = ResolveSemanticsSink(gui);
        if (sink is null)
        {
            _announcements.Clear();
            return;
        }
        var state = gui.HasFocus() ? ControlVisualState.Focused : ControlVisualState.None;
        sink.Publish(new ControlSemantics(canvas.Id, ControlRole.None, Name, null,
            ScreenBounds(canvas, canvas.Rect), state));
        var graphSink = sink as IGraphSemanticsSink;
        graphSink?.PublishGraph(new GraphElementSemantics(canvas.Id, null, null, null,
            new GraphElementView("", GraphElementRole.Graph, _frameCamera.VisibleBounds(_frameSize), Name),
            ScreenBounds(canvas, canvas.Rect), gui.HasFocus(), false, false, 0, _items.Length));
        var position = 0;
        foreach (var item in _items)
        {
            PublishElement(gui, canvas, sink, item, ++position);
        }
        foreach (var change in _announcements)
        {
            graphSink?.Announce(change);
        }
        _announcements.Clear();
    }

    static IControlSemanticsSink? ResolveSemanticsSink(Gui gui)
    {
        var sink = gui.ControlSemantics;
        if (sink is null && gui.Platform.TryGet<IAccessibilityCapability>(out var accessibility))
        {
            sink = accessibility;
        }
        return sink;
    }

    void PublishElement(Gui gui, LayoutNode canvas, IControlSemanticsSink sink, GraphElementView item, int position)
    {
        var local = _frameCamera.WorldToView(new GraphPoint(item.Bounds.X, item.Bounds.Y));
        var bounds = ScreenBounds(canvas, new Rect(canvas.Rect.X + local.X, canvas.Rect.Y + local.Y,
            (float)(item.Bounds.Width * _frameCamera.Zoom), (float)(item.Bounds.Height * _frameCamera.Zoom)));
        var focused = gui.HasFocus() && Navigation.FocusedKey == item.Key;
        var selected = Navigation.SelectedKeys.Contains(item.Key);
        var state = ElementState(focused, selected, item.Enabled);
        var id = ElementId(canvas.Id, item.Key);
        sink.Publish(new ControlSemantics(id, ControlRole.None, item.Name,
            string.Join(" ", new[] { item.TypeName, item.Error }.Where(static text => !string.IsNullOrEmpty(text))),
            bounds, state));
        (sink as IGraphSemanticsSink)?.PublishGraph(new GraphElementSemantics(id,
            item.ParentKey is null ? canvas.Id : ElementId(canvas.Id, item.ParentKey),
            item.SourceKey is null ? null : ElementId(canvas.Id, item.SourceKey),
            item.TargetKey is null ? null : ElementId(canvas.Id, item.TargetKey), item, bounds,
            focused, selected, !_visibleKeys.Contains(item.Key), position, _items.Length));
    }

    static ControlVisualState ElementState(bool focused, bool selected, bool enabled) =>
        (focused ? ControlVisualState.Focused : ControlVisualState.None)
        | (selected ? ControlVisualState.Selected : ControlVisualState.None)
        | (enabled ? ControlVisualState.None : ControlVisualState.Disabled);

    static string ElementId(string canvasId, string key) => $"{canvasId}/graph/{key}";

    static Rect ScreenBounds(LayoutNode node, Rect rect)
    {
        var a = node.LocalToScreen(rect.Position);
        var b = node.LocalToScreen(new Vector2(rect.X + rect.W, rect.Y));
        var c = node.LocalToScreen(rect.BottomRight);
        var d = node.LocalToScreen(new Vector2(rect.X, rect.Y + rect.H));
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new Rect(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }
}

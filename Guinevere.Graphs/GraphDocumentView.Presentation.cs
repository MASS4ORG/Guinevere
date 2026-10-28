using MASS4.Graph;

namespace Guinevere.Graphs;

/// <summary>What a domain presenter may draw in a known node's content slot.</summary>
/// <param name="Node">The real document node; its payload stays domain-owned.</param>
/// <param name="Type">The node's registered type.</param>
/// <param name="View">The node's view description.</param>
/// <param name="Selected">Whether the node is in the view selection.</param>
/// <param name="Detail">The frame's level of detail.</param>
/// <param name="Theme">The view's active theme.</param>
public sealed record GraphNodeVisual(
    GraphNode Node,
    NodeType Type,
    GraphElementView View,
    bool Selected,
    GraphLevelOfDetail Detail,
    GraphTheme Theme);

/// <summary>What a link renderer draws: the routed geometry and the view's default style for it.</summary>
/// <param name="Key">The edge key, or the preview key for an unfinished connection.</param>
/// <param name="Connector">The routed world geometry; draw relative to the current scope's bounds.</param>
/// <param name="Style">The default style for the link's state, theme and level of detail.</param>
/// <param name="Selected">Whether the link is selected or focused.</param>
/// <param name="Preview">Whether this is the transient connection preview.</param>
/// <param name="Detail">The frame's level of detail.</param>
public sealed record GraphLinkVisual(
    string Key,
    GraphConnector Connector,
    GraphLinkStyle Style,
    bool Selected,
    bool Preview,
    GraphLevelOfDetail Detail);

public sealed partial class GraphDocumentView
{
    const double NodeWidth = 220;
    const double RowHeight = 28;
    GraphTheme _theme = GraphTheme.Dark;

    /// <summary>Colors for nodes, ports and links; also applies its canvas style.</summary>
    public GraphTheme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            Canvas.Style = value.Canvas;
        }
    }

    /// <summary>How links travel between ports.</summary>
    public GraphLinkRouting Routing { get; set; }
    /// <summary>Draws arrowheads at link targets in full detail.</summary>
    public bool ShowArrows { get; set; } = true;
    /// <summary>Optional domain body drawn in a known node's content slot; unknown nodes never reach it.</summary>
    public Action<Gui, GraphNodeVisual>? NodePresenter { get; set; }
    /// <summary>Height of a known node's content slot in world units; none when absent or zero.</summary>
    public Func<GraphNode, double>? ContentHeight { get; set; }
    /// <summary>Optional domain badges for known nodes, drawn in the header.</summary>
    public Func<GraphNode, IEnumerable<string>>? NodeBadges { get; set; }
    /// <summary>Optional port marker shape; circles when absent.</summary>
    public Func<GraphPort, GraphPortShape>? PortShape { get; set; }
    /// <summary>Optional link renderer replacing the default styled stroke; hit testing is unchanged.</summary>
    public Action<Gui, GraphLinkVisual>? LinkRenderer { get; set; }
    /// <summary>Additional diagnostics, such as compile results, shown on the elements they name.</summary>
    public IReadOnlyList<Diagnostic> Validation { get; set; } = [];
    /// <summary>Pointer distance in logical screen units that still selects a link, independent of stroke width.</summary>
    public double LinkHitTolerance { get; set; } = 9;
    /// <summary>Pointer radius in logical screen units that still targets a port when finishing a drag.</summary>
    public double PortHitRadius { get; set; } = 18;

    /// <summary>
    /// The most severe diagnostic naming a node, one of its ports or an attached edge, from the last edit or
    /// <see cref="Validation"/>.
    /// </summary>
    public DiagnosticSeverity? NodeSeverity(ElementId node) => Severity(element => element == node
        || Editor.Document.TryGetPort(element, out var port) && port.NodeId == node
        || Editor.Document.TryGetEdge(element, out var edge) && (Owner(edge.Source) == node || Owner(edge.Target) == node));

    /// <summary>The most severe diagnostic naming an edge, from the last edit or <see cref="Validation"/>.</summary>
    public DiagnosticSeverity? EdgeSeverity(ElementId edge) => Severity(element => element == edge);

    DiagnosticSeverity? Severity(Func<ElementId, bool> names)
    {
        DiagnosticSeverity? worst = null;
        foreach (var diagnostic in Diagnostics.Concat(Validation))
        {
            if (diagnostic.ElementId is { } element && (worst is null || diagnostic.Severity > worst) && names(element))
            {
                worst = diagnostic.Severity;
            }
        }
        return worst;
    }

    ElementId? Owner(ElementId port) => Editor.Document.TryGetPort(port, out var found) ? found.NodeId : null;

    double NodeHeight(GraphNode node)
    {
        var content = Editor.Document.TryGetNodeType(node, out _) ? ContentHeight?.Invoke(node) ?? 0 : 0;
        GraphBounds.RequireFinite(content);
        return 56 + Rows(node) * RowHeight + (content > 0 ? content + 8 : 0);
    }

    void DrawNodeCard(Gui gui, GraphElementView element)
    {
        var rect = gui.CurrentNode.Rect;
        var node = Editor.Document.GetNode(ElementId.Parse(element.Key));
        var selected = Canvas.Navigation.SelectedKeys.Contains(element.Key);
        gui.DrawRectFilled(rect, selected ? _theme.NodeSelected : _theme.NodeBody, 10);
        var severity = element.Error is null ? NodeSeverity(node.Id) : DiagnosticSeverity.Error;
        gui.DrawRect(rect, 10).Stroke(SeverityColor(severity, _theme.NodeBorder), severity is null ? 1 : 2.5f);
        if (Canvas.Detail == GraphLevelOfDetail.Outline)
        {
            return;
        }
        gui.DrawRectFilled(new Rect(rect.X, rect.Y, rect.W, 40), _theme.NodeHeader, 10, Corner.Top);
        using (gui.Node(194, 26).Absolute(13, 10).Enter())
        {
            gui.DrawText(element.Name, 16, _theme.Title);
        }
        DrawBadges(gui, node, severity);
        if (Canvas.Detail == GraphLevelOfDetail.Full)
        {
            DrawContent(gui, node, element, selected);
        }
    }

    void DrawContent(Gui gui, GraphNode node, GraphElementView element, bool selected)
    {
        if (NodePresenter is not { } presenter || !Editor.Document.TryGetNodeType(node, out var type))
        {
            return;
        }
        var height = ContentHeight?.Invoke(node) ?? 0;
        if (height <= 0)
        {
            return;
        }
        using var content = gui.Node((float)(NodeWidth - 24), (float)height)
            .Absolute(12, (float)(48 + Rows(node) * RowHeight)).Enter();
        presenter(gui, new GraphNodeVisual(node, type, element, selected, Canvas.Detail, _theme));
    }

    Color SeverityColor(DiagnosticSeverity? severity, Color fallback) => severity switch
    {
        DiagnosticSeverity.Error => _theme.Error,
        DiagnosticSeverity.Warning => _theme.Warning,
        _ => fallback
    };

    static int Rows(GraphNode node)
    {
        var inputs = node.Ports.Count(static port => port.Direction == PortDirection.Input);
        return Math.Max(inputs, node.Ports.Count - inputs);
    }

    void DrawBadges(Gui gui, GraphNode node, DiagnosticSeverity? severity)
    {
        var badges = new List<(string Text, Color Color)>();
        if (severity is { } level and >= DiagnosticSeverity.Warning)
        {
            badges.Add(("!", level == DiagnosticSeverity.Error ? _theme.Error : _theme.Warning));
        }
        if (NodeBadges is { } domain && Editor.Document.TryGetNodeType(node, out _))
        {
            badges.AddRange(domain(node).Select(text => (text, _theme.Badge)));
        }
        // Build-pass rectangles are not laid out yet, so placement uses the fixed card width.
        var right = (float)NodeWidth - 10;
        foreach (var (text, color) in badges)
        {
            var width = Math.Max(18, gui.MeasureTextWidth(text, 11) + 10);
            right -= width;
            using var badge = gui.Node(width, 18).Absolute(right, -9).AlignContent(0.5f, 0.5f).Enter();
            gui.DrawBackgroundRect(color, 9);
            gui.DrawText(text, 11, _theme.BadgeText);
            right -= 4;
        }
    }

    void DrawPortMarker(Gui gui, GraphElementView element, GraphPort port)
    {
        var input = port.Direction == PortDirection.Input;
        var color = input ? _theme.InputPort : _theme.OutputPort;
        var center = gui.CurrentNode.Rect.Center;
        const float r = 7;
        switch (PortShape?.Invoke(port) ?? GraphPortShape.Circle)
        {
            case GraphPortShape.Square:
                gui.DrawRectFilled(new Rect(center.X - r, center.Y - r, r * 2, r * 2), color, 2);
                break;
            case GraphPortShape.Diamond:
                gui.DrawTriangleFilled(center - new Vector2(r, 0), center - new Vector2(0, r), center + new Vector2(r, 0),
                    color);
                gui.DrawTriangleFilled(center - new Vector2(r, 0), center + new Vector2(0, r), center + new Vector2(r, 0),
                    color);
                break;
            case GraphPortShape.Triangle:
                gui.DrawTriangleFilled(center + new Vector2(r, 0), center + new Vector2(-r, -r),
                    center + new Vector2(-r, r), color);
                break;
            default:
                gui.DrawCircleFilled(center, r, color);
                break;
        }
        if (Canvas.Detail != GraphLevelOfDetail.Full)
        {
            return;
        }
        // Labels extend over the card, so they pass the pointer through to the node beneath.
        var half = (float)element.Bounds.Width / 2;
        using var label = gui.Node(185, 20).Absolute(input ? half + 15 : half - 200, half - 11)
            .ContentAlignX(input ? 0 : 1).HitTestVisible(false).Enter();
        gui.DrawText(element.Name, 13, _theme.PortLabel).HitTestVisible(false);
    }

    void DrawLink(Gui gui, GraphElementView element)
    {
        var preview = element.Key == PreviewKey;
        var selected = Canvas.Navigation.SelectedKeys.Contains(element.Key)
            || Canvas.Navigation.FocusedKey == element.Key;
        var visual = new GraphLinkVisual(element.Key, _connectors[element.Key], LinkStyle(element, preview, selected),
            selected, preview, Canvas.Detail);
        if (LinkRenderer is { } renderer)
        {
            renderer(gui, visual);
        }
        else
        {
            visual.Connector.Draw(gui, visual.Style);
        }
    }

    GraphLinkStyle LinkStyle(GraphElementView element, bool preview, bool selected)
    {
        var detail = Canvas.Detail;
        if (preview)
        {
            return new GraphLinkStyle(_theme.LinkPreview, detail == GraphLevelOfDetail.Outline ? 1.5f : 2, Dash: 6);
        }
        var color = selected ? _theme.LinkSelected : SeverityColor(EdgeSeverity(ElementId.Parse(element.Key)), _theme.Link);
        var thickness = detail == GraphLevelOfDetail.Outline ? 1.5f : selected ? 3 : 2;
        return new GraphLinkStyle(color, thickness, ShowArrows && detail == GraphLevelOfDetail.Full);
    }
}

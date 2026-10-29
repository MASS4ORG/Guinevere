using MASS4.Graph;

namespace Guinevere.Graphs;

/// <summary>A known node's domain content slot, inheriting its PSS font, color, effects and tokens.</summary>
/// <param name="Node">The real document node; its payload stays domain-owned.</param>
/// <param name="Type">The node's registered type.</param>
/// <param name="View">The node's view description.</param>
/// <param name="Selected">Whether the node is selected.</param>
/// <param name="Detail">The frame's level of detail.</param>
/// <param name="Style">The node's resolved PSS declarations.</param>
public sealed record GraphNodeVisual(GraphNode Node, NodeType Type, GraphElementView View, bool Selected,
    GraphLevelOfDetail Detail, ResolvedStyle Style);

/// <summary>A routed link and the PSS stroke resolved for its current state.</summary>
/// <param name="Key">The edge key, or the transient preview key.</param>
/// <param name="Connector">World geometry drawn relative to the current scope.</param>
/// <param name="Style">The resolved stroke.</param>
/// <param name="Selected">Whether the link is selected or focused.</param>
/// <param name="Preview">Whether this is an unfinished connection.</param>
/// <param name="Detail">The frame's level of detail.</param>
public sealed record GraphLinkVisual(string Key, GraphConnector Connector, GraphLinkStyle Style, bool Selected,
    bool Preview, GraphLevelOfDetail Detail);

public sealed partial class GraphDocumentView
{
    readonly Dictionary<ElementId, ResolvedStyle> _nodeStyles = [];
    ResolvedStyle _headerStyle = GraphStyles.Defaults("graph-header");
    ResolvedStyle _contentStyle = GraphStyles.Defaults("graph-content");

    /// <summary>Application classes on the editor scope, available to every graph part and presenter.</summary>
    public IReadOnlyList<string> StyleClasses { get; set; } = [];
    /// <summary>Optional domain classes for known nodes; never called for unavailable node types.</summary>
    public Func<GraphNode, IReadOnlyList<string>>? NodeClasses { get; set; }
    /// <summary>How links travel between ports.</summary>
    public GraphLinkRouting Routing { get; set; }
    /// <summary>Draws arrowheads at link targets in full detail.</summary>
    public bool ShowArrows { get; set; } = true;
    /// <summary>Optional domain body in a known node's content slot.</summary>
    public Action<Gui, GraphNodeVisual>? NodePresenter { get; set; }
    /// <summary>Height of a known node's domain content slot in world units.</summary>
    public Func<GraphNode, double>? ContentHeight { get; set; }
    /// <summary>Optional domain badges for known nodes.</summary>
    public Func<GraphNode, IEnumerable<string>>? NodeBadges { get; set; }
    /// <summary>Optional marker shape for known nodes' ports; circles when absent.</summary>
    public Func<GraphPort, GraphPortShape>? PortShape { get; set; }
    /// <summary>Optional replacement for the PSS link stroke; hit testing is unchanged.</summary>
    public Action<Gui, GraphLinkVisual>? LinkRenderer { get; set; }
    /// <summary>Additional diagnostics, such as compilation findings.</summary>
    public IReadOnlyList<Diagnostic> Validation { get; set; } = [];
    /// <summary>Link selection tolerance in logical screen units, independent of stroke and zoom.</summary>
    public double LinkHitTolerance { get; set; } = 9;
    /// <summary>Port drop radius in logical screen units.</summary>
    public double PortHitRadius { get; set; } = 18;

    /// <summary>The worst diagnostic naming a node, one of its ports or an attached edge.</summary>
    public DiagnosticSeverity? NodeSeverity(ElementId node) => Severity(element => element == node
        || Editor.Document.TryGetPort(element, out var port) && port.NodeId == node
        || Editor.Document.TryGetEdge(element, out var edge) && (Owner(edge.Source) == node || Owner(edge.Target) == node));

    /// <summary>The worst diagnostic naming an edge.</summary>
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

    IReadOnlyList<string> Classes(GraphNode node) => CanPresent(node, out _)
        ? NodeClasses?.Invoke(node) ?? [] : [];

    ResolvedStyle NodeStyle(GraphNode node) => _nodeStyles.GetValueOrDefault(node.Id)
        ?? GraphStyles.Defaults("graph-node");
    double NodeWidth(GraphNode node) => GraphStyles.Length(NodeStyle(node), "width");
    double RowHeight(GraphNode node) => GraphStyles.Length(NodeStyle(node), "graph-port-spacing");
    double HeaderHeight => GraphStyles.Length(_headerStyle, "height");

    void ResolveGeometry(Gui gui)
    {
        _headerStyle = gui.ResolvePart("graph-header");
        _contentStyle = gui.ResolvePart("graph-content");
        _nodeStyles.Clear();
        foreach (var node in Editor.Document.Nodes)
        {
            _nodeStyles[node.Id] = gui.ResolvePart("graph-node", classes: Classes(node));
        }
    }

    double NodeHeight(GraphNode node)
    {
        var content = CanPresent(node, out _) ? ContentHeight?.Invoke(node) ?? 0 : 0;
        GraphBounds.RequireFinite(content);
        var style = NodeStyle(node);
        return HeaderHeight + Rows(node) * RowHeight(node) + GraphStyles.Length(style, "padding-top")
            + GraphStyles.Length(style, "padding-bottom")
            + (content > 0 ? content + GraphStyles.Length(_contentStyle, "margin-top") : 0);
    }

    List<string> NodeModifiers(GraphNode node, bool selected, bool unknown)
    {
        var modifiers = new List<string>();
        if (selected)
        {
            modifiers.Add("selected");
        }
        AddSeverity(modifiers, unknown ? DiagnosticSeverity.Error : NodeSeverity(node.Id));
        return modifiers;
    }

    static void AddSeverity(List<string> modifiers, DiagnosticSeverity? severity)
    {
        if (severity >= DiagnosticSeverity.Warning)
        {
            modifiers.Add(severity == DiagnosticSeverity.Error ? "error" : "warning");
        }
    }

    void DrawNodeCard(Gui gui, GraphElementView element)
    {
        var node = Editor.Document.GetNode(ElementId.Parse(element.Key));
        var selected = Canvas.Navigation.SelectedKeys.Contains(element.Key);
        var modifiers = NodeModifiers(node, selected, element.Error is not null);
        var style = gui.ResolvePart("graph-node", modifiers: modifiers, classes: Classes(node));
        using var card = gui.StyledNode("graph-node", classes: Classes(node), modifiers: modifiers)
            .Expand().Padding(0).HitTestVisible(false).Enter();
        if (Canvas.Detail == GraphLevelOfDetail.Outline)
        {
            return;
        }
        using (gui.StyledNode("graph-header").Width((float)element.Bounds.Width).Height((float)HeaderHeight)
                   .Absolute(0, 0).HitTestVisible(false).Enter())
        {
            var title = gui.ResolvePart("graph-title");
            using var text = gui.StyledNode("graph-title")
                .Width((float)element.Bounds.Width - GraphStyles.Length(title, "left") * 2)
                .Absolute(GraphStyles.Length(title, "left"), GraphStyles.Length(title, "top"))
                .HitTestVisible(false).Enter();
            gui.DrawText(element.Name).HitTestVisible(false);
        }
        DrawBadges(gui, node, element.Error is not null ? DiagnosticSeverity.Error : NodeSeverity(node.Id));
        if (Canvas.Detail == GraphLevelOfDetail.Full)
        {
            DrawContent(gui, node, element, selected, style);
        }
    }

    void DrawContent(Gui gui, GraphNode node, GraphElementView element, bool selected, ResolvedStyle style)
    {
        if (NodePresenter is not { } presenter || !CanPresent(node, out var type))
        {
            return;
        }
        var height = ContentHeight?.Invoke(node) ?? 0;
        if (height <= 0)
        {
            return;
        }
        var left = GraphStyles.Length(style, "padding-left");
        var right = GraphStyles.Length(style, "padding-right");
        using var content = gui.StyledNode("graph-content").Width((float)NodeWidth(node) - left - right)
            .Height((float)height).Margin(0).Absolute(left,
                (float)(HeaderHeight + Rows(node) * RowHeight(node) + GraphStyles.Length(style, "padding-top")))
            .Enter();
        presenter(gui, new GraphNodeVisual(node, type, element, selected, Canvas.Detail, style));
    }

    static int Rows(GraphNode node)
    {
        var inputs = node.Ports.Count(static port => port.Direction == PortDirection.Input);
        return Math.Max(inputs, node.Ports.Count - inputs);
    }

    void DrawBadges(Gui gui, GraphNode node, DiagnosticSeverity? severity)
    {
        var badges = new List<(string Text, string[] Modifiers)>();
        if (severity >= DiagnosticSeverity.Warning)
        {
            badges.Add(("!", [severity == DiagnosticSeverity.Error ? "error" : "warning"]));
        }
        if (NodeBadges is { } domain && CanPresent(node, out _))
        {
            badges.AddRange(domain(node).Select(static text => (text, Array.Empty<string>())));
        }
        var right = (float)NodeWidth(node) - GraphStyles.Length(gui.ResolvePart("graph-badge"), "right");
        foreach (var (text, modifiers) in badges)
        {
            var style = gui.ResolvePart("graph-badge", modifiers: modifiers);
            var height = GraphStyles.Length(style, "height");
            var badge = gui.StyledNode("graph-badge", modifiers: modifiers).Height(height)
                .Padding(0).AlignContent(0.5f, 0.5f).HitTestVisible(false);
            using var scope = badge.Enter();
            var width = Math.Max(GraphStyles.Length(style, "min-width"),
                gui.MeasureTextWidth(text)
                + GraphStyles.Length(style, "padding-left") + GraphStyles.Length(style, "padding-right"));
            right -= width;
            badge.Width(width).Absolute(right, -height / 2);
            gui.DrawText(text).HitTestVisible(false);
            right -= GraphStyles.Length(style, "gap");
        }
    }

    void DrawPortMarker(Gui gui, GraphElementView element, GraphPort port)
    {
        var input = port.Direction == PortDirection.Input;
        var modifiers = new List<string> { input ? "input" : "output" };
        if (Canvas.Navigation.ConnectionSourceKey is { } source && source != element.Key)
        {
            modifiers.Add(CanConnect(ElementId.Parse(source), port.Id) ? "compatible" : "incompatible");
        }
        var style = gui.ResolvePart("graph-port", modifiers: modifiers);
        var r = GraphStyles.Length(style, "width") / 2;
        var owner = Editor.Document.GetNode(port.NodeId);
        var shape = CanPresent(owner, out _) ? PortShape?.Invoke(port) : null;
        DrawMarker(gui, (float)element.Bounds.Width / 2, r, shape ?? GraphPortShape.Circle, style);
        if (Canvas.Detail == GraphLevelOfDetail.Full)
        {
            DrawPortLabel(gui, element, input);
        }
    }

    static void DrawMarker(Gui gui, float half, float r, GraphPortShape shape, ResolvedStyle style)
    {
        var marker = gui.Node(r * 2, r * 2).Absolute(half - r, half - r).HitTestVisible(false);
        using var scope = marker.Enter();
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }
        var clip = MarkerClip(shape, r);
        if (clip is not null)
        {
            gui.SetClipArea(marker, clip);
        }
        gui.DrawStyledBox(style, marker.Rect);
        var border = GraphStyles.Length(style, "border-width");
        if (border > 0 && clip is not null)
        {
            gui.DrawShape(marker.Rect.Center, clip).Stroke(GraphStyles.Color(style, "border-color"), border * 2);
        }
    }

    static Shape? MarkerClip(GraphPortShape shape, float radius) => shape switch
    {
        GraphPortShape.Square => null,
        GraphPortShape.Diamond => Shape.Rect(-radius / MathF.Sqrt(2), -radius / MathF.Sqrt(2),
            radius / MathF.Sqrt(2), radius / MathF.Sqrt(2)).Rotate(45),
        GraphPortShape.Triangle => Shape.Triangle(radius, 0, -radius, -radius, -radius, radius),
        _ => Shape.Circle(radius)
    };

    static void DrawPortLabel(Gui gui, GraphElementView element, bool input)
    {
        var style = gui.ResolvePart("graph-port-label");
        var width = GraphStyles.Length(style, "width");
        var height = GraphStyles.Length(style, "height");
        var gap = GraphStyles.Length(style, "margin");
        var half = (float)element.Bounds.Width / 2;
        using var label = gui.StyledNode("graph-port-label").Margin(0).Width(width).Height(height)
            .Absolute(input ? half + gap : half - width - gap, half - height / 2)
            .ContentAlignX(input ? 0 : 1).HitTestVisible(false).Enter();
        gui.DrawText(element.Name).HitTestVisible(false);
    }

    void DrawLink(Gui gui, GraphElementView element)
    {
        var preview = element.Key == PreviewKey;
        var selected = Canvas.Navigation.SelectedKeys.Contains(element.Key) || Canvas.Navigation.FocusedKey == element.Key;
        var modifiers = LinkModifiers(element, preview, selected);
        var style = gui.ResolvePart("graph-link", modifiers: modifiers);
        using var scope = gui.StyledNode("graph-link", modifiers: modifiers).Expand().HitTestVisible(false).Enter();
        var stroke = new GraphLinkStyle(GraphStyles.Color(style, "stroke"), GraphStyles.Length(style, "stroke-width"),
            !preview && ShowArrows && Canvas.Detail == GraphLevelOfDetail.Full,
            GraphStyles.Length(style, "stroke-dasharray"), GraphStyles.Length(style, "stroke-dashoffset"),
            GraphStyles.Dashes(style), GraphStyles.Length(style, "marker-width"));
        var visual = new GraphLinkVisual(element.Key, _connectors[element.Key], stroke, selected, preview, Canvas.Detail);
        if (LinkRenderer is { } renderer)
        {
            renderer(gui, visual);
        }
        else
        {
            visual.Connector.Draw(gui, visual.Style);
        }
    }

    List<string> LinkModifiers(GraphElementView element, bool preview, bool selected)
    {
        var modifiers = new List<string>();
        if (selected)
        {
            modifiers.Add("selected");
        }
        if (preview)
        {
            modifiers.Add("preview");
        }
        if (Canvas.Detail == GraphLevelOfDetail.Outline)
        {
            modifiers.Add("outline");
        }
        if (!preview)
        {
            AddSeverity(modifiers, EdgeSeverity(ElementId.Parse(element.Key)));
        }
        return modifiers;
    }
}

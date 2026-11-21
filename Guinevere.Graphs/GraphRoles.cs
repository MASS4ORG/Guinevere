namespace Guinevere.Graphs;

/// <summary>
/// Accessibility roles of graph elements. Each specializes a standard <see cref="ControlRole"/>, so native
/// adapters that know nothing about graphs still present a canvas, panels, buttons and links.
/// </summary>
public static class GraphRoles
{
    /// <summary>The graph surface.</summary>
    public static ControlRole Graph { get; } = new("Graph", ControlRole.Canvas);
    /// <summary>A node represented by a domain adapter.</summary>
    public static ControlRole Node { get; } = new("Node", ControlRole.Panel);
    /// <summary>A connection endpoint owned by a node.</summary>
    public static ControlRole Port { get; } = new("Port", ControlRole.Button);
    /// <summary>A connection between two endpoints.</summary>
    public static ControlRole Edge { get; } = new("Edge", ControlRole.Link);
    /// <summary>An authored frame containing nodes and nested groups.</summary>
    public static ControlRole Group { get; } = new("Group", ControlRole.Group);

    /// <summary>Returns the accessibility role for a graph element role.</summary>
    public static ControlRole For(GraphElementRole role) => role switch
    {
        GraphElementRole.Graph => Graph,
        GraphElementRole.Node => Node,
        GraphElementRole.Port => Port,
        GraphElementRole.Edge => Edge,
        GraphElementRole.Group => Group,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}

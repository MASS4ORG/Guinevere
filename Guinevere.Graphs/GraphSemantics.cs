namespace Guinevere.Graphs;

/// <summary>The graph-specific role of a logical view element.</summary>
public enum GraphElementRole
{
    /// <summary>The graph surface.</summary>
    Graph,
    /// <summary>A node represented by a domain adapter.</summary>
    Node,
    /// <summary>A connection endpoint owned by a node.</summary>
    Port,
    /// <summary>A connection between two endpoints.</summary>
    Edge
}

/// <summary>
/// Read-only view metadata supplied by an adapter, including conservative world bounds for culling.
/// Keys are opaque view identities; payloads, registries, validation, and document edits remain outside the canvas.
/// </summary>
public sealed record GraphElementView(
    string Key,
    GraphElementRole Role,
    GraphBounds Bounds,
    string Name,
    string? ParentKey = null,
    string? TypeName = null,
    string? Error = null,
    string? SourceKey = null,
    string? TargetKey = null,
    bool Enabled = true);

/// <summary>A logical graph element, including virtualized elements which have no layout control.</summary>
public sealed record GraphElementSemantics(
    string Id,
    string? ParentId,
    string? SourceId,
    string? TargetId,
    GraphElementView View,
    Rect ScreenBounds,
    bool Focused,
    bool Selected,
    bool Virtualized,
    int Position,
    int Count);

/// <summary>Semantic changes and announcements available to automation and platform accessibility adapters.</summary>
public enum GraphSemanticEventKind
{
    /// <summary>Logical graph focus changed.</summary>
    FocusChanged,
    /// <summary>View selection changed.</summary>
    SelectionChanged,
    /// <summary>A source port was chosen.</summary>
    ConnectionStarted,
    /// <summary>A pending connection was dismissed.</summary>
    ConnectionCancelled,
    /// <summary>An adapter completed a connection command.</summary>
    ConnectionCreated,
    /// <summary>An adapter completed a delete command.</summary>
    ElementDeleted,
    /// <summary>An edit was rejected or has no registered handler.</summary>
    ValidationFailed,
    /// <summary>A searchable command or node creation surface was requested.</summary>
    SearchRequested
}

/// <summary>A semantic change with optional related element and an accessible announcement.</summary>
public sealed record GraphSemanticEvent(GraphSemanticEventKind Kind, string? Key, string? RelatedKey, string Message);

/// <summary>
/// Optional richer graph metadata layered on Guinevere's existing control semantics capability.
/// A platform bridge may implement this alongside <see cref="IAccessibilityCapability"/>.
/// </summary>
public interface IGraphSemanticsSink : IControlSemanticsSink
{
    /// <summary>Publishes logical roles, relationships, and positions without materializing layout controls.</summary>
    void PublishGraph(GraphElementSemantics semantics);

    /// <summary>Announces a change emitted by the graph interaction state.</summary>
    void Announce(GraphSemanticEvent change);
}

/// <summary>The model command a domain adapter is asked to execute through its own editor.</summary>
public enum GraphEditKind
{
    /// <summary>Connect two logical ports.</summary>
    Connect,
    /// <summary>Delete a logical element.</summary>
    Delete
}

/// <summary>An edit intent; the canvas never applies model mutations itself.</summary>
public sealed record GraphEditRequest(GraphEditKind Kind, string Key, string? TargetKey = null);

/// <summary>The adapter's command outcome, including validation or MG diagnostic text for announcement.</summary>
public sealed record GraphEditResult(bool Succeeded, string Message);

/// <summary>Search surfaces requested by keyboard or application commands.</summary>
public enum GraphSearchKind
{
    /// <summary>Search the application's graph commands.</summary>
    Commands,
    /// <summary>Search node types supplied by the domain registry adapter.</summary>
    CreateNode
}

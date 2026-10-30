namespace Guinevere.Graphs;

sealed class GraphNativeAccessibility(IAccessibilityTreeSink sink, IAccessibilityActionSource? source)
    : IGraphSemanticsSink, IGraphActionSource
{
    /// <inheritdoc />
    public void Publish(ControlSemantics semantics)
    {
        if (sink is IControlSemanticsSink controls)
        {
            controls.Publish(semantics);
        }
    }

    /// <inheritdoc />
    public void PublishGraph(GraphElementSemantics semantics)
    {
        var view = semantics.View;
        sink.PublishElement(new AccessibilityElement(semantics.Id, semantics.ParentId, view.Role.ToString(),
            view.Name, Description(view), semantics.ScreenBounds, State(semantics), semantics.Virtualized,
            semantics.Actions?.Select(static a => a.ToString()).ToArray(), Scope(semantics),
            view.Role == GraphElementRole.Graph ? null : view.Key,
            semantics.SourceId, semantics.TargetId, Expanded(view)));
    }

    static string Description(GraphElementView view) => string.Join("; ",
        new[] { view.Role.ToString(), view.TypeName, view.StateDescription, view.Error }
            .Where(static s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.Ordinal));

    static string Scope(GraphElementSemantics semantics) => semantics.View.Role == GraphElementRole.Graph ? semantics.Id
        : semantics.Id[..semantics.Id.LastIndexOf("/graph/", StringComparison.Ordinal)];

    static ControlVisualState State(GraphElementSemantics semantics) =>
        (semantics.Focused ? ControlVisualState.Focused : ControlVisualState.None)
        | (semantics.Selected ? ControlVisualState.Selected : ControlVisualState.None)
        | (semantics.View.Enabled ? ControlVisualState.None : ControlVisualState.Disabled);

    static bool? Expanded(GraphElementView view) => view.Role == GraphElementRole.Group
        ? view.StateDescription?.StartsWith("Expanded", StringComparison.Ordinal) : null;

    /// <inheritdoc />
    public void Announce(GraphSemanticEvent change) => sink.Announce(change.GraphId ?? "", change.Message,
        change.Kind == GraphSemanticEventKind.FocusChanged && change.Key is { } key
            ? $"{change.GraphId}/graph/{key}" : null);

    /// <inheritdoc />
    public bool TryDequeue(string graphId, out GraphAction action)
    {
        if (source?.TryDequeue(graphId, out var request) == true
            && Enum.TryParse<GraphActionKind>(request.Name, out var kind))
        {
            action = new GraphAction(kind == GraphActionKind.Connect ? GraphActionKind.Activate : kind, request.Key);
            return true;
        }
        action = null!;
        return false;
    }
}

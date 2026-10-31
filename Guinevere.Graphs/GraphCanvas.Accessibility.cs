namespace Guinevere.Graphs;

public sealed partial class GraphCanvas
{
    static readonly GraphActionKind[] SurfaceActions =
        [GraphActionKind.Focus, GraphActionKind.SearchCommands, GraphActionKind.CreateNode, GraphActionKind.Cancel];
    static readonly GraphActionKind[] ItemActions =
        [GraphActionKind.Focus, GraphActionKind.Select, GraphActionKind.ToggleSelection, GraphActionKind.Delete];
    static readonly GraphActionKind[] NodeActions = [.. ItemActions, GraphActionKind.Activate];
    static readonly GraphActionKind[] PortActions = [.. NodeActions, GraphActionKind.Connect];

    void ReadAccessibilityInput(Gui gui, string id)
    {
        var source = ResolveActionSource(gui);
        if (source is null)
        {
            return;
        }
        for (var i = 0; i < 64 && source.TryDequeue(id, out var request); i++)
        {
            var action = request;
            _pendingInput.Enqueue(() => ApplyAccessibilityAction(gui, id, action));
            gui.RequestFrame();
        }
    }

    static IGraphActionSource? ResolveActionSource(Gui gui)
    {
        if (gui.Platform.TryGet<IGraphActionSource>(out var source))
        {
            return source;
        }
        return ResolveSemanticsSink(gui) as IGraphActionSource;
    }

    void ApplyAccessibilityAction(Gui gui, string id, GraphAction action)
    {
        if (action.Key is { } key && !Navigation.Focus(key))
        {
            Announce(new(GraphSemanticEventKind.ValidationFailed, key, null, "The graph element is unavailable."));
            return;
        }
        gui.Focus.RequestFocus(id, FocusReason.Keyboard);
        if (EditorActionRequested?.Invoke(gui, action) == true)
        {
            return;
        }
        ApplyLogicalAction(action);
    }

    void ApplyLogicalAction(GraphAction action)
    {
        switch (action.Kind)
        {
            case GraphActionKind.Focus:
                break;
            case GraphActionKind.Select:
                if (action.Key is { } selected)
                {
                    Navigation.Select(selected);
                }

                break;
            case GraphActionKind.ToggleSelection:
                Navigation.ToggleSelection();
                break;
            case GraphActionKind.Activate:
                Navigation.Activate();
                break;
            case GraphActionKind.Connect:
                Navigation.Connect(action.Key, action.TargetKey);
                break;
            default:
                ApplySurfaceAction(action.Kind);
                break;
        }
    }

    void ApplySurfaceAction(GraphActionKind kind)
    {
        switch (kind)
        {
            case GraphActionKind.Delete:
                Navigation.Delete();
                break;
            case GraphActionKind.Cancel:
                Navigation.Back();
                break;
            case GraphActionKind.SearchCommands:
                Navigation.Search(GraphSearchKind.Commands);
                break;
            case GraphActionKind.CreateNode:
                Navigation.Search(GraphSearchKind.CreateNode);
                break;
            default:
                Announce(new(GraphSemanticEventKind.ValidationFailed, null, null, "No editor handles this action."));
                break;
        }
    }

    IReadOnlyList<GraphActionKind> AvailableActions(GraphElementView item)
    {
        if (!item.Enabled)
        {
            return [];
        }
        return item.Role switch
        {
            GraphElementRole.Graph => [.. SurfaceActions, .. EditorActions?.Invoke() ?? []],
            GraphElementRole.Node => NodeActions,
            GraphElementRole.Group => NodeActions,
            GraphElementRole.Port => PortActions,
            _ => ItemActions
        };
    }
}

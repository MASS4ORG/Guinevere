using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    IReadOnlyList<Diagnostic> _validation = [];
    bool _navigationEdit;

    static readonly Dictionary<GraphActionKind, Action<GraphDocumentView>> EditorOperations = new()
    {
        [GraphActionKind.Undo] = static view => view.Undo(),
        [GraphActionKind.Redo] = static view => view.Redo(),
        [GraphActionKind.SelectAll] = static view => view.SelectAll(),
        [GraphActionKind.Delete] = static view => view.DeleteSelection()
    };

    /// <summary>Executes a domain command through the real editor and publishes its result to accessibility.</summary>
    public CommandResult Execute(IGraphCommand command) => Accept(Editor.Execute(command));

    void SetValidation(IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (_validation.SequenceEqual(diagnostics))
        {
            return;
        }
        _validation = diagnostics.ToArray();
        var message = diagnostics.Count == 0 ? "Graph validation cleared." : DiagnosticMessage(diagnostics);
        var kind = diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error)
            ? GraphSemanticEventKind.ValidationFailed : GraphSemanticEventKind.ValidationChanged;
        Canvas.Announce(new(kind, diagnostics.FirstOrDefault()?.ElementId?.ToString(), null, message));
    }

    void AnnounceResult(CommandResult result, string? message)
    {
        if (_navigationEdit)
        {
            return;
        }
        AnnounceDiagnostics(result);
        if (result.Succeeded)
        {
            Canvas.Announce(new(GraphSemanticEventKind.CommandCompleted, null, null,
                message ?? Editor.NextUndo?.Description ?? "Graph command completed."));
        }
    }

    void AnnounceDiagnostics(CommandResult result)
    {
        if (result.Diagnostics.Count > 0)
        {
            var kind = result.Succeeded ? GraphSemanticEventKind.ValidationChanged : GraphSemanticEventKind.ValidationFailed;
            Canvas.Announce(new(kind, result.Diagnostics[0].ElementId?.ToString(), null, DiagnosticMessage(result.Diagnostics)));
        }
    }

    static string DiagnosticMessage(IEnumerable<Diagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(static d => $"{d.Id}: {d.Message}"));

    string? DiagnosticText(Func<ElementId, bool> matches)
    {
        var findings = Diagnostics.Concat(Validation).Where(d => d.ElementId is { } id && matches(id)).Distinct().ToArray();
        return findings.Length > 0 ? DiagnosticMessage(findings) : null;
    }

    IReadOnlyList<GraphActionKind> EditorSemanticActions()
    {
        var actions = new List<GraphActionKind> { GraphActionKind.SelectAll, GraphActionKind.Paste };
        if (Editor.CanUndo)
        {
            actions.Add(GraphActionKind.Undo);
        }

        if (Editor.CanRedo)
        {
            actions.Add(GraphActionKind.Redo);
        }

        if (Canvas.Navigation.SelectedKeys.Count > 0)
        {
            actions.AddRange([GraphActionKind.Delete, GraphActionKind.Copy, GraphActionKind.Cut]);
        }
        return actions;
    }

    bool HandleAccessibilityAction(Gui gui, GraphAction action)
    {
        if (EditorOperations.TryGetValue(action.Kind, out var operation))
        {
            if (action.Kind == GraphActionKind.Delete && action.Key is { } key
                && !Canvas.Navigation.SelectedKeys.Contains(key))
            {
                Canvas.Navigation.Select(key);
            }
            operation(this);
            Refresh();
            return true;
        }
        return HandleAccessibleClipboard(gui, action.Kind);
    }

    bool HandleAccessibleClipboard(Gui gui, GraphActionKind kind) => kind switch
    {
        GraphActionKind.Copy => ClipboardKey(gui, KeyboardKey.C),
        GraphActionKind.Cut => ClipboardKey(gui, KeyboardKey.X),
        GraphActionKind.Paste => PasteAccessible(gui),
        GraphActionKind.Cancel => CancelEditingGesture() || Canvas.Navigation.Back(),
        _ => false
    };

    bool PasteAccessible(Gui gui)
    {
        var handled = ClipboardKey(gui, KeyboardKey.V);
        Refresh();
        return handled;
    }

    string PortState(GraphPort port)
    {
        var direction = port.Direction == PortDirection.Input ? "Input" : "Output";
        var multiplicity = port.Declaration.Multiplicity == PortMultiplicity.Single ? "single connection" : "multiple connections";
        var text = $"{direction}; {multiplicity}; {Editor.Document.GetEdges(port.Id).Count} connected.";
        if (Canvas.Navigation.ConnectionSourceKey == port.Id.ToString())
        {
            text += " Connection source.";
        }
        return text;
    }

    string EdgeName(GraphEdge edge) => $"{EndpointName(edge.Source)} to {EndpointName(edge.Target)}";

    string EndpointName(ElementId id)
    {
        var port = Editor.Document.GetPort(id);
        return $"{DescribeNode(Editor.Document.GetNode(port.NodeId)).Title} {port.Declaration.Name}";
    }

    bool NamesNode(ElementId node, ElementId element) => element == node
        || Editor.Document.TryGetPort(element, out var port) && port.NodeId == node
        || Editor.Document.TryGetEdge(element, out var edge) && (Owner(edge.Source) == node || Owner(edge.Target) == node);
}

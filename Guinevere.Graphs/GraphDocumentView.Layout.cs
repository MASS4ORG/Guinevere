using MASS4.Graph;

namespace Guinevere.Graphs;

public sealed partial class GraphDocumentView
{
    Dictionary<ElementId, GraphPoint>? _dragStart;

    Dictionary<ElementId, GraphPoint> SnapshotLayout() => Editor.Document.Nodes
        .ToDictionary(static node => node.Id, node => EnsurePosition(node.Id));

    void CommitLayout(string description, Dictionary<ElementId, GraphPoint> before)
    {
        var edits = before.Where(pair => _positions[pair.Key] != pair.Value)
            .Select(pair => new LayoutEdit(pair.Key, pair.Value, _positions[pair.Key])).ToArray();
        if (edits.Length > 0)
        {
            Accept(Editor.Execute(new LayoutCommand(this, description, edits)));
        }
    }

    void BeginNodeDrag(DragEvent e)
    {
        if (e.Button == MouseButton.Left)
        {
            _dragStart = SnapshotLayout();
            DragNodes(e);
        }
    }

    void EndNodeDrag(DragEvent e)
    {
        if (e.Button == MouseButton.Left && _dragStart is { } before)
        {
            _dragStart = null;
            CommitLayout("Move nodes", before);
            e.StopPropagation();
        }
    }

    readonly record struct LayoutEdit(ElementId Node, GraphPoint Before, GraphPoint After);

    // Empty document changes keep view edits out of persistence and evaluation invalidation.
    sealed class LayoutCommand(GraphDocumentView view, string description, LayoutEdit[] edits) : IGraphCommand
    {
        /// <inheritdoc />
        public string Description => description;

        /// <inheritdoc />
        public bool Apply(GraphDocument graph, DiagnosticBag diagnostics)
        {
            foreach (var edit in edits)
            {
                view._positions[edit.Node] = edit.After;
            }
            return true;
        }

        /// <inheritdoc />
        public void Revert(GraphDocument graph)
        {
            foreach (var edit in edits)
            {
                view._positions[edit.Node] = edit.Before;
            }
        }
    }
}

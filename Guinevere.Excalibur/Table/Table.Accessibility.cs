using System.Globalization;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Publishes the table's semantic tree for the rows and columns it built this frame: the table, a column
    /// header per shown column, and for every row in view a row with a cell per shown column, each with a name,
    /// a role and its state. Rows outside the window are not built and so not published, which is what a
    /// virtualized list must do; the table's description carries the full counts. It runs in the render pass
    /// and only when the host offers an accessibility tree.
    /// </summary>
    static void PublishTableSemantics<TRow>(in TableBuild<TRow> b)
    {
        var (gui, state, _, shown, options, _, _, layout, _, _) = b;
        if (state.Accessibility is not { } sink)
        {
            return;
        }

        var tableId = state.AccessibleId!;
        var selection = state.Selection;
        var selected = selection.Count;
        var description = string.Create(CultureInfo.InvariantCulture,
            $"{state.FrameRowCount} rows, {shown.Length} columns, {selected} selected");
        sink.PublishElement(new AccessibilityElement(tableId, null, "Table", options.AccessibleName, description,
            gui.CurrentNode.Rect, gui.HasFocus(BodyId) ? ControlVisualState.Focused : ControlVisualState.None));

        for (var i = 0; i < layout.Columns.Length; i++)
        {
            var column = layout.Columns[i];
            var sort = state.Sort.IndexOf(column) is var at and >= 0
                ? state.Sort[at].Direction == TableSortDirection.Ascending ? "sorted ascending" : "sorted descending"
                : "";
            sink.PublishElement(new AccessibilityElement($"{tableId}/column/{column.Id}", tableId, "ColumnHeader",
                column.Title.Length > 0 ? column.Title : column.Id, sort, Rect.Zero, ControlVisualState.None,
                Virtualized: true, ScopeId: tableId, Key: column.Id));
        }

        AnnounceChanges(state, sink, tableId, selected);
    }

    /// <summary>Publishes one built row and its cells; called while the row's node is current.</summary>
    static void PublishRowSemantics<TRow>(in TableBuild<TRow> b, TableRowSlot slot, TRow item, int row, string key)
    {
        var (gui, state, _, shown, _, typed, _, _, _, _) = b;
        if (state.Accessibility is not { } sink)
        {
            return;
        }

        var tableId = state.AccessibleId;
        var rowId = $"{tableId}/row/{key}";
        var current = state.Selection.CurrentKey == key && gui.HasFocus(BodyId);
        var rowState = (state.Selection.Contains(key) ? ControlVisualState.Selected : ControlVisualState.None)
                       | (current ? ControlVisualState.Focused : ControlVisualState.None);
        bool? expanded = typed?.RowDetails is { } details
                         && details.HasDetails(new TableRowContext<TRow>(row, item, new RowIdentity(key), state))
            ? state.Expansion.IsExpanded(key)
            : null;
        var name = shown.Length > 0 ? shown[0].Text(item) : "";
        sink.PublishElement(new AccessibilityElement(rowId, tableId, "Row",
            name.Length > 0 ? name : $"Row {row + 1}", $"Row {row + 1} of {state.FrameRowCount}",
            gui.CurrentNode.Rect, rowState, ScopeId: tableId, Key: key, Expanded: expanded));
    }

    /// <summary>Publishes one built cell, a child of its row; called while the cell's node is current.</summary>
    static void PublishCellSemantics<TRow>(in TableBuild<TRow> b, TableRowSlot slot, TRow item, int index)
    {
        var (gui, state, _, shown, _, _, _, _, _, _) = b;
        if (state.Accessibility is not { } sink)
        {
            return;
        }

        var column = shown[index];
        var tableId = state.AccessibleId!;
        var rowId = $"{tableId}/row/{slot.Key}";
        var cellState = (state.Selection.Contains(slot.Key!) ? ControlVisualState.Selected : ControlVisualState.None);
        sink.PublishElement(new AccessibilityElement($"{rowId}/cell/{column.Id}", rowId, "Cell", column.Text(item),
            column.Title, gui.CurrentNode.Rect, cellState, ScopeId: tableId, Key: slot.Key));
    }

    /// <summary>Announces what the user just did that no element's state says: a new selection or a new sort.</summary>
    static void AnnounceChanges(TableState state, IAccessibilityTreeSink sink, string tableId, int selected)
    {
        if (state.AnnouncedSelection != state.Selection.Version)
        {
            state.AnnouncedSelection = state.Selection.Version;
            sink.Announce(tableId, selected switch
            {
                0 => "Selection cleared",
                1 => "1 row selected",
                _ => $"{selected} rows selected"
            });
        }

        if (state.AnnouncedSort != state.Sort.Version)
        {
            state.AnnouncedSort = state.Sort.Version;
            if (state.Sort.Primary is { } primary)
            {
                sink.Announce(tableId, $"Sorted by {primary.Column.Title}, " +
                                       (primary.Direction == TableSortDirection.Ascending ? "ascending" : "descending"));
            }
        }
    }
}

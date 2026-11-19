namespace Guinevere;

/// <summary>What a table does and says, apart from how big its parts are (<see cref="TableTheme"/>).</summary>
public sealed class TableOptions
{
    /// <summary>
    /// Builds only the columns in view when the table scrolls sideways. It pays for itself with hundreds
    /// of columns; with a few it only adds spacers.
    /// </summary>
    public bool VirtualizeColumns { get; init; }

    /// <summary>
    /// Lets the user drag a header's right edge, or press Shift with the arrow keys on a focused header, to
    /// change the column's width. A column opts out with <see cref="TableColumn.Resizable"/>.
    /// </summary>
    public bool Resizable { get; init; } = true;

    /// <summary>
    /// Lets the user drag a header onto another to move the column, or press Ctrl with the arrow keys on a
    /// focused header. A column moves only among those pinned to the same edge.
    /// </summary>
    public bool Reorderable { get; init; } = true;

    /// <summary>Opens a menu of the columns, to show or hide each, when the user right-clicks the header.</summary>
    public bool ColumnMenu { get; init; } = true;

    /// <summary>The column menu's entry that puts the order, visibility and widths back to the definitions.</summary>
    public string ResetColumnsText { get; init; } = "Reset columns";

    /// <summary>Shown in place of the rows when the source has none.</summary>
    public string EmptyText { get; init; } = "No rows";

    /// <summary>Shown while the source is loading, in place of the rows or below them.</summary>
    public string LoadingText { get; init; } = "Loading…";

    /// <summary>The default options.</summary>
    public static TableOptions Default { get; } = new();
}

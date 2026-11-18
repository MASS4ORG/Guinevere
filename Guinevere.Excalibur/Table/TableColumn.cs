using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>Where a column's content sits horizontally inside its cells.</summary>
public enum TableAlignment
{
    /// <summary>At the leading edge.</summary>
    Left,

    /// <summary>Centered.</summary>
    Center,

    /// <summary>At the trailing edge, as numbers read best.</summary>
    Right
}

/// <summary>Whether a column stays in view while the rest of the table scrolls sideways.</summary>
public enum TablePin
{
    /// <summary>Scrolls with the other columns.</summary>
    None,

    /// <summary>Stays at the left edge.</summary>
    Left,

    /// <summary>Stays at the right edge.</summary>
    Right
}

/// <summary>
/// One column of a table. A definition never changes after it is built, so a table can share one list
/// of columns between frames and callers can key persisted column state on <see cref="Id"/>.
/// </summary>
public sealed class TableColumn
{
    /// <summary>Creates a column.</summary>
    /// <param name="title">The header text.</param>
    /// <param name="id">
    /// Stable key for column state and for the source's <c>GetValue</c>; unique in a table. Left out, it is
    /// derived from the declaring file's name and line. That derived key changes when the line moves, which
    /// silently orphans any persisted state, so give every column whose state is persisted across sessions
    /// an explicit id. Columns created on one line, in a loop for instance, need explicit ids as well.
    /// </param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public TableColumn(string title, TableColumnId? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(title);
        var key = id ?? TableColumnId.FromSite(filePath, lineNumber);
        ArgumentException.ThrowIfNullOrEmpty(key.Value, nameof(id));
        Id = key;
        Title = title;
    }

    /// <summary>Stable key for column state and for the source's <c>GetValue</c>.</summary>
    public TableColumnId Id { get; }

    /// <summary>The header text.</summary>
    public string Title { get; }

    /// <summary>How the width is decided; an equal share of the free width by default.</summary>
    public TableLength Width { get; init; } = TableLength.Star();

    /// <summary>The narrowest the column may be, in pixels.</summary>
    public float MinWidth { get; init; } = 48f;

    /// <summary>The widest the column may be, in pixels.</summary>
    public float MaxWidth { get; init; } = float.PositiveInfinity;

    /// <summary>Where the header and cell content sit inside the column.</summary>
    public TableAlignment Alignment { get; init; } = TableAlignment.Left;

    /// <summary>Whether the column stays at an edge while the rest scrolls sideways.</summary>
    public TablePin Pin { get; init; } = TablePin.None;

    /// <summary>Whether the column is shown before the user has chosen otherwise.</summary>
    public bool InitiallyVisible { get; init; } = true;

    /// <summary>Draws the column's cells, or null for the default text rendering of the value.</summary>
    public ITableCellRenderer? Renderer { get; init; }
}

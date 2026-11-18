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
    /// <param name="id">Stable key for column state and for the source's <c>GetValue</c>; unique in a table.</param>
    /// <param name="title">The header text.</param>
    public TableColumn(string id, string title)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(title);
        Id = id;
        Title = title;
    }

    /// <summary>Stable key for column state and for the source's <c>GetValue</c>.</summary>
    public string Id { get; }

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

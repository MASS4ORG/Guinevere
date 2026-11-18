namespace Guinevere;

/// <summary>What a table does and says, apart from how big its parts are (<see cref="TableTheme"/>).</summary>
public sealed class TableOptions
{
    /// <summary>
    /// Builds only the columns in view when the table scrolls sideways. It pays for itself with hundreds
    /// of columns; with a few it only adds spacers.
    /// </summary>
    public bool VirtualizeColumns { get; init; }

    /// <summary>Shown in place of the rows when the source has none.</summary>
    public string EmptyText { get; init; } = "No rows";

    /// <summary>Shown while the source is loading, in place of the rows or below them.</summary>
    public string LoadingText { get; init; } = "Loading…";

    /// <summary>The default options.</summary>
    public static TableOptions Default { get; } = new();
}

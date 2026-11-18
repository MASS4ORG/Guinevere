namespace Guinevere;

/// <summary>Layout metrics for a table; stylesheet rules define its appearance.</summary>
public sealed class TableTheme
{
    /// <summary>Height of one body row; every row is the same height, which keeps scrolling arithmetic exact.</summary>
    public float RowHeight { get; init; } = 24f;

    /// <summary>Height of the header row.</summary>
    public float HeaderHeight { get; init; } = 28f;

    /// <summary>Cell and header text size.</summary>
    public float FontSize { get; init; } = 12f;

    /// <summary>Space between a cell's edge and its content, on each side.</summary>
    public float CellPadding { get; init; } = 8f;

    /// <summary>Rows kept built above and below the viewport, so a fast scroll has no gap.</summary>
    public int RowOverscan { get; init; } = 4;

    /// <summary>Columns kept built beside the viewport when columns are virtualised.</summary>
    public int ColumnOverscan { get; init; } = 1;

    /// <summary>The default theme.</summary>
    public static TableTheme Default { get; } = new();
}

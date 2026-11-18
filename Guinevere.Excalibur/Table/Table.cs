using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    static readonly string[] OddRow = ["odd"];

    /// <summary>
    /// Draws a scrollable table over a counted, indexed data source. Only the rows in the viewport, and
    /// with <see cref="TableOptions.VirtualizeColumns"/> only the columns in it, become layout nodes, so a
    /// table of millions of rows costs the same as one that fills the screen. The header stays in view
    /// while the rows scroll and follows them sideways.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="state">What the table keeps between frames, held by the caller.</param>
    /// <param name="source">The data, read by row index and only for cells in view.</param>
    /// <param name="columns">The column definitions, in display order. Share one list between frames.</param>
    /// <param name="options">Behavior and wording. Defaults to <see cref="TableOptions.Default"/>.</param>
    /// <param name="theme">Layout metrics. Defaults to <see cref="TableTheme.Default"/>.</param>
    /// <param name="classes">Stylesheet classes for this control.</param>
    /// <param name="id">Stable control and stylesheet identity.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void Table(this Gui gui, TableState state, ITableViewSource source,
        IReadOnlyList<TableColumn> columns, TableOptions? options = null, TableTheme? theme = null,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(columns);
        ExcaliburStyles.Ensure(gui);

        options ??= TableOptions.Default;
        theme ??= TableTheme.Default;

        var shown = ShownColumns(state, columns);
        var layout = TableLayout.Resolve(shown, state.FrameViewportWidth, AutoWidths(gui, state, theme, shown));
        var (first, last) = options.VirtualizeColumns
            ? layout.Window(state.FrameScrollX, state.FrameViewportWidth, theme.ColumnOverscan)
            : (0, shown.Length);

        using (gui.StyledNode("table", classes, id, filePath: filePath, lineNumber: lineNumber)
                   .Expand().Direction(Axis.Vertical).Enter())
        {
            TableHeader(gui, state, theme, layout, first, last);
            TableBody(gui, state, source, options, theme, layout, first, last);
        }
    }

    /// <summary>The columns the table lays out: those visible by default, in the order given.</summary>
    static TableColumn[] ShownColumns(TableState state, IReadOnlyList<TableColumn> columns)
    {
        if (!ReferenceEquals(state.CheckedColumns, columns))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in columns)
            {
                if (!ids.Add(column.Id))
                {
                    throw new ArgumentException($"Table column id '{column.Id}' is used twice.", nameof(columns));
                }
            }

            state.CheckedColumns = columns;
        }

        var shown = new List<TableColumn>(columns.Count);
        foreach (var column in columns)
        {
            if (column.InitiallyVisible)
            {
                shown.Add(column);
            }
        }

        return [.. shown];
    }

    static float[] AutoWidths(Gui gui, TableState state, TableTheme theme, TableColumn[] shown)
    {
        var widths = new float[shown.Length];
        for (var i = 0; i < shown.Length; i++)
        {
            var column = shown[i];
            if (column.Width.Kind != TableLengthKind.Auto)
            {
                continue;
            }

            widths[i] = state.AutoWidth(column, theme.FontSize, () =>
                MeasureTextWidth(MeasuringFont(gui, theme.FontSize), column.Title) + (theme.CellPadding * 2f));
        }

        return widths;
    }

    static void TableBody(Gui gui, TableState state, ITableViewSource source, TableOptions options,
        TableTheme theme, TableLayout layout, int firstColumn, int lastColumn)
    {
        using (gui.StyledNode("table-body", id: "table/body").Expand().Direction(Axis.Vertical).Enter())
        {
            gui.Scroll();

            var count = source.RowCount;
            if (count == 0)
            {
                Placeholder(gui, source.IsLoading ? "table-loading" : "table-empty", theme,
                    source.IsLoading ? options.LoadingText : options.EmptyText);
            }
            else
            {
                var window = VirtualRows.Window(state.FrameScrollY, state.FrameViewportHeight, theme.RowHeight,
                    count, theme.RowOverscan);

                VirtualRows.Spacer(gui, "table/padTop", window.PadTop);
                for (var row = window.First; row < window.Last; row++)
                {
                    TableRow(gui, source, theme, layout, row, firstColumn, lastColumn);
                }

                VirtualRows.Spacer(gui, "table/padBottom", window.PadBottom);

                if (source.IsLoading)
                {
                    Placeholder(gui, "table-loading", theme, options.LoadingText, theme.RowHeight);
                }
            }

            if (gui.Pass == Pass.Pass2Render)
            {
                Measure(gui, state);
            }
        }
    }

    /// <summary>Records the body's viewport and offset for the next frame, and the width columns share.</summary>
    static void Measure(Gui gui, TableState state)
    {
        var measure = VirtualRows.Measure(gui);
        if (measure.Height > 0)
        {
            state.FrameViewportHeight = measure.Height;
        }

        if (measure.Width > 0)
        {
            state.FrameViewportWidth = measure.Width;
        }

        state.FrameScrollX = measure.X;
        state.FrameScrollY = measure.Y;
    }

    static void Placeholder(Gui gui, string element, TableTheme theme, string text, float? height = null)
    {
        using (gui.StyledNode(element, id: $"table/{element}").ExpandWidth().Height(height ?? theme.RowHeight * 3f)
                   .ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            gui.DrawText(text, theme.FontSize, centerInRect: false);
        }
    }
}

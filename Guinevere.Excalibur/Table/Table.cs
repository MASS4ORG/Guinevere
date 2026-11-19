using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    const string EmptyId = "table/table-empty";
    const string LoadingId = "table/table-loading";

    static readonly string[] OddRow = ["odd"];

    /// <summary>
    /// Draws a scrollable table over a counted, indexed data source. Only the rows in the viewport, and
    /// with <see cref="TableOptions.VirtualizeColumns"/> only the columns in it, become layout nodes, so a
    /// table of millions of rows costs the same as one that fills the screen. The header stays in view
    /// while the rows scroll and follows them sideways.
    /// </summary>
    /// <typeparam name="TRow">The type of a row, as the source's indexer returns it.</typeparam>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="state">What the table keeps between frames, held by the caller.</param>
    /// <param name="source">The rows, read by index and only for rows in view.</param>
    /// <param name="columns">The columns, built once and shared between frames.</param>
    /// <param name="options">Behavior and wording. Defaults to <see cref="TableOptions.Default"/>.</param>
    /// <param name="theme">Layout metrics. Defaults to <see cref="TableTheme.Default"/>.</param>
    /// <param name="classes">Stylesheet classes for this control.</param>
    /// <param name="id">Stable control and stylesheet identity.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void Table<TRow>(this Gui gui, TableState state, ITableViewSource<TRow> source,
        TableColumns<TRow> columns, TableOptions? options = null, TableTheme? theme = null,
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

        state.BeginFrame(gui, source);
        if (state.Cache.Begin(columns.Set, state.Columns))
        {
            state.Sort.Rebind(columns.Set);
        }

        var layout = ResolveLayout(gui, state, theme);
        var (first, last) = options.VirtualizeColumns
            ? layout.Window(state.FrameScrollX, state.FrameViewportWidth, theme.ColumnOverscan)
            : (layout.LeftCount, layout.RightStart);

        using (gui.StyledNode("table", classes, id, filePath: filePath, lineNumber: lineNumber)
                   .Expand().Direction(Axis.Vertical).Enter())
        {
            TableHeader(gui, state, options, theme, layout, first, last);
            TableBody(gui, state, source, state.Cache.View!.Typed<TRow>(), options, theme, layout, first, last);
        }
    }

    /// <summary>
    /// The column geometry for this frame, reused until the columns, the width they share or the text
    /// metrics change. Header-sized widths are measured once per column and metrics.
    /// </summary>
    static TableLayout ResolveLayout(Gui gui, TableState state, TableTheme theme)
    {
        var cache = state.Cache;
        var width = state.FrameViewportWidth;
        if (cache.TryLayout(width, theme.FontSize, theme.CellPadding, out var layout))
        {
            return layout;
        }

        var view = cache.View!;
        var shown = view.Shown;
        var autoWidths = new float[shown.Length];
        for (var i = 0; i < shown.Length; i++)
        {
            var column = shown[i];
            if (view.Lengths[i].Kind != TableLengthKind.Auto)
            {
                continue;
            }

            if (!cache.TryAutoWidth(column, theme.FontSize, theme.CellPadding, out autoWidths[i]))
            {
                autoWidths[i] = MeasureTextWidth(MeasuringFont(gui, theme.FontSize), column.Title)
                                + (theme.CellPadding * 2f);
                cache.StoreAutoWidth(column, theme.FontSize, theme.CellPadding, autoWidths[i]);
            }
        }

        layout = TableLayout.Resolve(shown, view.Lengths, width, autoWidths);
        cache.StoreLayout(layout, width, theme.FontSize, theme.CellPadding);
        return layout;
    }

    static void TableBody<TRow>(Gui gui, TableState state, ITableViewSource<TRow> source,
        TableColumn<TRow>[] shown, TableOptions options, TableTheme theme, TableLayout layout, int firstColumn,
        int lastColumn)
    {
        using (gui.StyledNode("table-body", id: "table/body").Expand().Direction(Axis.Vertical).Enter())
        {
            gui.Scroll();

            // The snapshot taken at the start of the frame, not the source's current values.
            var count = state.FrameRowCount;
            if (count == 0)
            {
                if (state.FrameLoading)
                {
                    Placeholder(gui, "table-loading", LoadingId, theme, options.LoadingText);
                }
                else
                {
                    Placeholder(gui, "table-empty", EmptyId, theme, options.EmptyText);
                }
            }
            else
            {
                var window = VirtualRows.Window(state.FrameScrollY, state.FrameViewportHeight, theme.RowHeight,
                    count, theme.RowOverscan);
                state.Cache.Reserve(window.Last - window.First);

                VirtualRows.Spacer(gui, "table/padTop", window.PadTop);
                for (var row = window.First; row < window.Last; row++)
                {
                    TableRow(gui, state, source, shown, theme, layout, row, firstColumn, lastColumn);
                }

                VirtualRows.Spacer(gui, "table/padBottom", window.PadBottom);

                if (state.FrameLoading)
                {
                    Placeholder(gui, "table-loading", LoadingId, theme, options.LoadingText, theme.RowHeight);
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

    static void Placeholder(Gui gui, string element, string id, TableTheme theme, string text, float? height = null)
    {
        using (gui.StyledNode(element, id: id).ExpandWidth().Height(height ?? theme.RowHeight * 3f)
                   .AlignContentX(0.5f).AlignContentY(0.5f).Enter())
        {
            gui.DrawText(text, theme.FontSize, centerInRect: false);
        }
    }
}

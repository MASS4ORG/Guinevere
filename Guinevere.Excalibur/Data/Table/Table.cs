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
    /// <param name="enabled">When false the table matches <c>:disabled</c> and ignores pointer and keyboard input.</param>
    /// <param name="classes">Stylesheet classes for this control.</param>
    /// <param name="id">Stable control and stylesheet identity.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>
    /// The table's node, <c>true</c> as a <c>bool</c> on the frame the selection or the sort changed, or a row was
    /// activated (<see cref="LayoutNode.Changed"/> and <see cref="LayoutNode.Clicked"/> after a chain).
    /// </returns>
    public static ControlNode Table<TRow>(this Gui gui, TableState state, ITableViewSource<TRow> source,
        TableColumns<TRow> columns, TableOptions? options = null, bool enabled = true,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(columns);
        ExcaliburStyles.Ensure(gui);

        options ??= TableOptions.Default;

        state.BeginFrame(gui, source);
        state.Cache.SourceVersion = state.FrameSourceVersion;
        if (state.Cache.Begin(columns.Set, state.Columns))
        {
            state.Sort.Rebind(columns.Set);
        }

        var typed = options as TableOptions<TRow>;
        state.Details = typed?.RowDetails;

        var node = gui.StyledControl("table", classes, id, disabled: !enabled, filePath: filePath,
            lineNumber: lineNumber);
        node.Expand().Direction(Axis.Vertical);
        UseFontSize(gui, node, TableMetrics.DefaultFontSize);
        using (node.Enter())
        {
            var metrics = TableMetrics.Read(gui, node.Scope.Get<LayoutNodeScopeTextSize>().Value);
            state.Geometry.Update(gui, state, source, typed?.RowDetails, metrics.RowHeight);

            var layout = ResolveLayout(gui, state, metrics);
            var (first, last) = options.VirtualizeColumns
                ? layout.Window(state.FrameScrollX, state.FrameViewportWidth, options.ColumnOverscan)
                : (layout.LeftCount, layout.RightStart);
            var build = new TableBuild<TRow>(gui, state, source, state.Cache.View!.Typed<TRow>(), options, typed,
                metrics, layout, first, last, node, enabled);
            var selectionVersion = state.Selection.Version;
            var sortVersion = state.Sort.Version;
            state.Accessibility = gui.Pass == Pass.Pass2Render && gui.Platform.TryGet<IAccessibilityTreeSink>(out var tree)
                ? tree
                : null;
            state.AccessibleId = gui.CurrentNode.Id;
            TableHeader(build);
            TableBody(build);
            RowContextMenu(build);
            PublishTableSemantics(build);
            if (gui.Pass == Pass.Pass2Render
                && (state.Selection.Version != selectionVersion || state.Sort.Version != sortVersion))
            {
                node.Raise(NodeSignals.Changed);
            }
        }

        return node;
    }

    /// <summary>
    /// The column geometry for this frame, reused until the columns, the width they share or the text
    /// metrics change. Header-sized widths are measured once per column and metrics.
    /// </summary>
    static TableLayout ResolveLayout(Gui gui, TableState state, TableMetrics metrics)
    {
        var cache = state.Cache;
        var width = state.FrameViewportWidth;
        if (cache.TryLayout(width, metrics.FontSize, metrics.CellPadding, out var layout))
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

            if (!cache.TryAutoWidth(column, metrics.FontSize, metrics.CellPadding, out autoWidths[i]))
            {
                autoWidths[i] = MeasureTextWidth(MeasuringFont(gui, metrics.FontSize), column.Title)
                                + (metrics.CellPadding * 2f);
                cache.StoreAutoWidth(column, metrics.FontSize, metrics.CellPadding, autoWidths[i]);
            }
        }

        layout = TableLayout.Resolve(shown, view.Lengths, width, autoWidths);
        cache.StoreLayout(layout, width, metrics.FontSize, metrics.CellPadding);
        return layout;
    }

    static void TableBody<TRow>(in TableBuild<TRow> b)
    {
        var (gui, state, _, _, options, _, metrics, _, _, _, _, enabled) = b;
        using (gui.StyledNode("table-body", id: BodyId).Expand().Direction(Axis.Vertical).Enter())
        {
            gui.Scroll();
            if (gui.Pass == Pass.Pass2Render && enabled && options.Selection != TableSelectionMode.None)
            {
                gui.RegisterFocusable(claimsArrowKeys: true);
            }

            // The snapshot taken at the start of the frame, not the source's current values.
            var count = state.FrameRowCount;
            if (count == 0)
            {
                if (state.FrameLoading)
                {
                    Placeholder(gui, "table-loading", LoadingId, metrics, options.LoadingText);
                }
                else
                {
                    Placeholder(gui, "table-empty", EmptyId, metrics, options.EmptyText);
                }
            }
            else
            {
                var offsets = state.Geometry.Offsets;
                var window = offsets is null
                    ? VirtualRows.Window(state.FrameScrollY, state.FrameViewportHeight, metrics.RowHeight, count,
                        options.RowOverscan)
                    : VirtualRows.Window(state.FrameScrollY, state.FrameViewportHeight, offsets, options.RowOverscan);
                state.Cache.Reserve(window.Last - window.First);

                VirtualRows.Spacer(gui, "table/padTop", window.PadTop);
                for (var row = window.First; row < window.Last; row++)
                {
                    TableRow(b, row);
                }

                VirtualRows.Spacer(gui, "table/padBottom", window.PadBottom);

                if (state.FrameLoading)
                {
                    Placeholder(gui, "table-loading", LoadingId, metrics, options.LoadingText, metrics.RowHeight);
                }
            }

            if (gui.Pass == Pass.Pass2Render)
            {
                if (!gui.Input.IsMouseButtonDown(MouseButton.Left))
                {
                    state.DragSelecting = false;
                }

                BodyKeys(b);
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

    static void Placeholder(Gui gui, string element, string id, TableMetrics metrics, string text, float? height = null)
    {
        using (gui.StyledNode(element, id: id).ExpandWidth().Height(height ?? metrics.RowHeight * 3f)
                   .AlignContentX(0.5f).AlignContentY(0.5f).Enter())
        {
            gui.DrawText(text, metrics.FontSize, centerInRect: false);
        }
    }
}

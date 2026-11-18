using Guinevere;

namespace Controls01;

public abstract partial class Program
{
    const int TableRows = 1_000_000;
    const int TableExtraColumns = 40;

    static readonly string[] TableStatuses = ["Ready", "Building", "Failed"];

    static readonly TableColumn<int, int> TableRowColumn = TableColumn<int>.Of("Row", static row => row + 1,
        id: "row", width: 90f, alignment: TableAlignment.Right);

    static readonly TableColumns<int> TableDemoColumns = BuildTableColumns();
    static readonly TableState TableDemoState = BuildTableState();
    static readonly TableOptions TableDemoOptions = new() { VirtualizeColumns = true };
    static readonly GeneratedRows TableDemoSource = new(TableRows);

    static TableState BuildTableState()
    {
        var state = new TableState();
        state.Sort.Set([new TableSort(TableRowColumn)]);
        return state;
    }

    /// <summary>
    /// The rows are plain indices, so each column reads its value by arithmetic on the row: no strings dispatch
    /// the value, and a typo in a column cannot compile.
    /// </summary>
    static TableColumns<int> BuildTableColumns()
    {
        var columns = new List<TableColumn<int>>
        {
            TableRowColumn,
            TableColumn<int>.Of("Name", static row => $"asset_{row:D7}.png", width: TableLength.Star(2),
                minWidth: 160f),
            TableColumn<int>.Of("Size (KB)", static row => (row * 37 % 9000) + 12, id: "size", width: 100f,
                alignment: TableAlignment.Right),
            TableColumn<int>.Of("Status", static row => TableStatuses[row % TableStatuses.Length], width: 90f,
                renderer: new StatusRenderer())
        };

        for (var i = 0; i < TableExtraColumns; i++)
        {
            var number = i + 1;
            columns.Add(TableColumn<int>.Of($"Extra {number}", row => $"Extra {number} · {row}",
                id: $"extra{number}", width: 110f));
        }

        return new TableColumns<int>(columns);
    }

    static void TableDemo(Gui gui)
    {
        using (gui.Node().Expand().Padding(10).Direction(Axis.Vertical).Gap(6).Enter())
        {
            gui.DrawText($"{TableRows:N0} rows, {TableDemoColumns.Count} columns", size: 18);
            gui.DrawText("Only the cells in view are built. Wheel scrolls, Shift+wheel scrolls sideways; the header stays put.",
                size: 13, color: Token(gui, "text-dim"));

            using (gui.Node().Expand().Enter())
            {
                gui.Table(TableDemoState, TableDemoSource, TableDemoColumns, TableDemoOptions);
            }
        }
    }

    /// <summary>A million generated rows that exist only as the row index.</summary>
    sealed class GeneratedRows(int count) : ITableViewSource<int>
    {
        public int RowCount => count;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public int this[int index] => index;

        public RowIdentity GetRowIdentity(int index) => RowIdentity.FromIndex(index);
    }

    /// <summary>Tints the status text, as a column's own renderer would draw a badge.</summary>
    sealed class StatusRenderer : ITableCellRenderer<int, string>
    {
        public void Render(Gui gui, in TableCellContext<int, string> cell)
        {
            var color = cell.Value switch
            {
                "Ready" => Token(gui, "positive"),
                "Failed" => Token(gui, "negative"),
                _ => Token(gui, "warning")
            };
            gui.DrawText(cell.Value, cell.FontSize, color, centerInRect: false);
        }
    }
}

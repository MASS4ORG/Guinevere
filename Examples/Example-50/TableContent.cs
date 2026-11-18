using Guinevere;

namespace Controls01;

public abstract partial class Program
{
    const int TableRows = 1_000_000;
    const int TableExtraColumns = 40;

    static readonly TableState TableDemoState = new() { Sort = new TableSort(new TableColumnId("row")) };
    static readonly TableOptions TableDemoOptions = new() { VirtualizeColumns = true };
    static readonly TableColumn[] TableDemoColumns = BuildTableColumns();
    static readonly GeneratedRows TableDemoSource = new(TableRows);

    static TableColumn[] BuildTableColumns()
    {
        var columns = new List<TableColumn>
        {
            new("Row", new("row")) { Width = 90f, Alignment = TableAlignment.Right },
            new("Name", new("name")) { Width = TableLength.Star(2), MinWidth = 160f },
            new("Size (KB)", new("size")) { Width = 100f, Alignment = TableAlignment.Right },
            new("Status", new("status")) { Width = 90f, Renderer = new StatusRenderer() }
        };

        for (var i = 0; i < TableExtraColumns; i++)
        {
            columns.Add(new TableColumn($"Extra {i + 1}", new($"extra{i}")) { Width = 110f });
        }

        return [.. columns];
    }

    static void TableDemo(Gui gui)
    {
        using (gui.Node().Expand().Padding(10).Direction(Axis.Vertical).Gap(6).Enter())
        {
            gui.DrawText($"{TableRows:N0} rows, {TableDemoColumns.Length} columns", size: 18);
            gui.DrawText("Only the cells in view are built. Wheel scrolls, Shift+wheel scrolls sideways; the header stays put.",
                size: 13, color: Token(gui, "text-dim"));

            using (gui.Node().Expand().Enter())
            {
                gui.Table(TableDemoState, TableDemoSource, TableDemoColumns, TableDemoOptions);
            }
        }
    }

    /// <summary>A million generated rows that exist only as arithmetic on the row index.</summary>
    sealed class GeneratedRows(int count) : ITableViewSource
    {
        static readonly string[] Statuses = ["Ready", "Building", "Failed"];

        public int RowCount => count;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public object? GetValue(int row, TableColumn column) => column.Id.Value switch
        {
            "row" => row + 1,
            "name" => $"asset_{row:D7}.png",
            "size" => (row * 37 % 9000) + 12,
            "status" => Statuses[row % Statuses.Length],
            _ => $"{column.Title} · {row}"
        };

        public RowIdentity GetRowIdentity(int row) => RowIdentity.FromIndex(row);
    }

    /// <summary>Tints the status text, as a column's own renderer would draw a badge.</summary>
    sealed class StatusRenderer : ITableCellRenderer
    {
        public void Render(Gui gui, in TableCellContext cell)
        {
            var text = cell.Value as string ?? "";
            var color = text switch
            {
                "Ready" => Token(gui, "positive"),
                "Failed" => Token(gui, "negative"),
                _ => Token(gui, "warning")
            };
            gui.DrawText(text, cell.FontSize, color, centerInRect: false);
        }
    }
}

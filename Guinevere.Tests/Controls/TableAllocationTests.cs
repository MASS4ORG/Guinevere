using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Holds the table's steady-state allocation at its measured baseline. The limits sit about 15% above the
/// post-trim measurement (static 138 KB and scrolling 215 KB a frame, Debug and Release alike, with the
/// engine's own floor for the same nodes at 85 KB), so a change that raises them has to say why in the
/// wiki and move the limit on purpose. Zero allocation is the engine's goal, not the table's.
/// </summary>
public class TableAllocationTests
{
    const int WarmFrames = 60;
    const int MeasuredFrames = 300;

    [Fact]
    public void AStaticTableStaysWithinItsAllocationBaseline()
    {
        var bytes = BytesPerFrame(scroll: false);

        Assert.True(bytes < 150_000, $"{bytes} B/frame; baseline is about 138000");
    }

    [Fact]
    public void AScrollingTableStaysWithinItsAllocationBaseline()
    {
        var bytes = BytesPerFrame(scroll: true);

        Assert.True(bytes < 230_000, $"{bytes} B/frame; baseline is about 215000");
    }

    static long BytesPerFrame(bool scroll)
    {
        TableColumn[] columns = [new("A", new("a")), new("B", new("b")), new("C", new("c"))];
        var state = new TableState();
        var source = new RowSource(1_000_000);
        using var harness = new FrameHarness(560, 400);

        void Draw(Gui gui) => gui.Table(state, source, columns);
        void Step()
        {
            if (scroll)
            {
                harness.Gui.ScrollBy("table/body", new Vector2(0, 72));
            }

            harness.Frame(Draw);
        }

        for (var frame = 0; frame < WarmFrames; frame++)
        {
            Step();
        }

        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < MeasuredFrames; frame++)
        {
            Step();
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / MeasuredFrames;
    }

    sealed class RowSource(int rows) : ITableViewSource
    {
        public int RowCount => rows;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public object? GetValue(int row, TableColumn column) => row;

        public RowIdentity GetRowIdentity(int row) => RowIdentity.FromIndex(row);
    }
}

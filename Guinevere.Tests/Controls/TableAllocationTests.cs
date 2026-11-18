using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Holds the table's steady-state allocation at its measured best. Each limit is that best plus 10%, set once
/// on 2026-11-18 (best in Debug, the configuration the gate runs in: static 127.2 KB and scrolling 211.1 KB a
/// frame; Release is lower still at 114.6 KB and 198.7 KB). The scenario runs twice in the process and only the
/// second run counts, because the first pays one-time costs and measured 5% to 10% higher. The policy is in the
/// wiki page guinevere-table-allocation: a change that raises a number has to say why there and move the limit
/// on purpose, and the margin is not to be widened again to absorb noise. Zero allocation is the engine's goal,
/// not the table's.
/// </summary>
public class TableAllocationTests
{
    const int WarmFrames = 60;
    const int MeasuredFrames = 300;

    [Fact]
    public void AStaticTableStaysWithinItsAllocationBaseline()
    {
        var bytes = SteadyBytesPerFrame(scroll: false);

        Assert.True(bytes < 140_000, $"{bytes} B/frame; best measured is 127200, limit is best + 10%");
    }

    [Fact]
    public void AScrollingTableStaysWithinItsAllocationBaseline()
    {
        var bytes = SteadyBytesPerFrame(scroll: true);

        Assert.True(bytes < 233_000, $"{bytes} B/frame; best measured is 211100, limit is best + 10%");
    }

    static long SteadyBytesPerFrame(bool scroll)
    {
        _ = BytesPerFrame(scroll);
        return BytesPerFrame(scroll);
    }

    static long BytesPerFrame(bool scroll)
    {
        // Every column reads the row number, as the baseline was measured: the engine shapes each distinct
        // text once, so columns of different text would add engine cost (about 12 KB a frame per new text).
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row),
            TableColumn<int>.Of("B", static row => row),
            TableColumn<int>.Of("C", static row => row)
        ]);
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

    sealed class RowSource(int rows) : ITableViewSource<int>
    {
        public int RowCount => rows;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public int this[int index] => index;

        public RowIdentity GetRowIdentity(int row) => RowIdentity.FromIndex(row);
    }
}

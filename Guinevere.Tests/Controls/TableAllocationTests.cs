using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Holds the table's steady-state allocation near its measurement in Debug, the configuration the gate runs in
/// (static 131.8 KB and scrolling 215.8 KB a frame, up from 127.2 and 211.1 for the resize grab zones, the
/// per-row identity and the interaction hooks); the limits are the earlier best plus 10% and stay. The scenario runs twice in
/// the process and only the second run counts, because the first pays one-time costs. A change that raises a
/// number moves the limit on purpose and says why in the wiki page guinevere-table-allocation. Zero allocation is
/// the engine's goal, not the table's.
/// </summary>
public class TableAllocationTests
{
    const int WarmFrames = 60;
    const int MeasuredFrames = 300;

    [Fact]
    public void AStaticTableStaysWithinItsAllocationBaseline()
    {
        var bytes = SteadyBytesPerFrame(scroll: false);

        Assert.True(bytes < 140_000, $"{bytes} B/frame; measured 131800 with column interaction and identity-keyed ids, limit 140000");
    }

    [Fact]
    public void AScrollingTableStaysWithinItsAllocationBaseline()
    {
        var bytes = SteadyBytesPerFrame(scroll: true);

        Assert.True(bytes < 233_000, $"{bytes} B/frame; measured 215800 with column interaction and identity-keyed ids, limit 233000");
    }

    [Fact]
    public void ATableWhoseColumnIsBeingResizedEveryFrameStaysWithinItsAllocationBaseline()
    {
        var bytes = SteadyBytesPerFrame(scroll: false, resize: true);

        Assert.True(bytes < 168_000, $"{bytes} B/frame; measured 152600 while a column width changes every frame, limit is that plus 10%");
    }

    [Fact]
    public void ASelectableScrollingTableStaysWithinItsAllocationBaseline()
    {
        var bytes = SteadyBytesPerFrame(scroll: true, select: true);

        Assert.True(bytes < 242_000, $"{bytes} B/frame; measured 219800 while a selectable table scrolls, limit is that plus 10%");
    }

    [Fact]
    public void AScrollingTableDoesNotGrowTheIdentityTableWithTheRowsScrolled()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row),
            TableColumn<int>.Of("B", static row => row),
            TableColumn<int>.Of("C", static row => row)
        ]);
        var state = new TableState();
        var source = new RowSource(1_000_000);
        using var harness = new FrameHarness(560, 400);
        harness.Gui.IdentityRetainPasses = 64;
        void Draw(Gui gui) => gui.Table(state, source, columns);

        // Measured: the table holds about 1,070 identities at most (the built rows and cells plus the ones kept for
        // 64 passes), reached within 500 frames and never exceeded in the next 2,500. A leak would add one
        // row's worth for each of the 9,000 rows scrolled past, so the bound has room for noise and not for that.
        const int Bound = 1_500;
        var early = 0;
        var late = 0;
        for (var frame = 0; frame < 3_000; frame++)
        {
            harness.Gui.ScrollBy("table/body", new Vector2(0, 72));
            harness.Frame(Draw);
            var count = harness.Gui.IdentityCount;
            if (frame < 500)
            {
                early = Math.Max(early, count);
            }
            else
            {
                late = Math.Max(late, count);
            }
        }

        Assert.True(late < Bound, $"{late} identities after 3000 frames, limit {Bound}");
        Assert.True(late <= early, $"identities grew from {early} to {late} while scrolling");
    }

    static long SteadyBytesPerFrame(bool scroll, bool resize = false, bool select = false)
    {
        _ = BytesPerFrame(scroll, resize, select);
        return BytesPerFrame(scroll, resize, select);
    }

    static long BytesPerFrame(bool scroll, bool resize = false, bool select = false)
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

        var options = select ? new TableOptions { Selection = TableSelectionMode.Multiple } : null;
        void Draw(Gui gui) => gui.Table(state, source, columns, options);
        var tick = 0;
        void Step()
        {
            if (scroll)
            {
                harness.Gui.ScrollBy("table/body", new Vector2(0, 72));
            }

            if (resize)
            {
                state.Columns.SetWidth(columns[0], TableLength.Pixels(100f + (tick++ % 2 * 10f)));
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

        public RowIdentity GetRowIdentity(int row) => RowIdentity.UnstableIndex(row);
    }
}

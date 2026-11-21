namespace Guinevere.Tests.Controls;

/// <summary>Covers where rows sit when some rows are followed by extra height, and what a window builds over them.</summary>
public class RowOffsetIndexTests
{
    [Fact]
    public void WithNoExtraHeightEveryRowIsOneStrideTall()
    {
        var index = new RowOffsetIndex(10, 20f);

        Assert.Equal(200f, index.Total);
        Assert.Equal(60f, index.TopOf(3));
        Assert.Equal(3, index.RowAt(60f));
        Assert.Equal(3, index.RowAt(79.9f));
    }

    [Fact]
    public void AnExpandedRowPushesTheRowsBelowItDown()
    {
        var index = new RowOffsetIndex(10, 20f);
        index.Set(5, 40f);

        Assert.Equal(100f, index.TopOf(5));
        Assert.Equal(160f, index.TopOf(6));
        Assert.Equal(240f, index.Total);
    }

    [Fact]
    public void APositionInsideAnExpandedRowsExtraHeightBelongsToThatRow()
    {
        var index = new RowOffsetIndex(10, 20f);
        index.Set(5, 40f);

        Assert.Equal(4, index.RowAt(99.9f));
        Assert.Equal(5, index.RowAt(100f));
        Assert.Equal(5, index.RowAt(150f));
        Assert.Equal(6, index.RowAt(160f));
        Assert.Equal(9, index.RowAt(239.9f));
    }

    [Fact]
    public void PositionsOutsideTheListClampToItsFirstAndLastRow()
    {
        var index = new RowOffsetIndex(10, 20f);
        index.Set(9, 30f);

        Assert.Equal(0, index.RowAt(-5f));
        Assert.Equal(9, index.RowAt(10_000f));
        Assert.Equal(9, index.RowAt(index.Total));
    }

    [Fact]
    public void EveryRowIsFoundAtItsTopAndAtItsLastPositionWithSeveralExpandedRows()
    {
        var index = new RowOffsetIndex(40, 24f);
        index.Set(0, 10f);
        index.Set(7, 60f);
        index.Set(8, 5f);
        index.Set(39, 100f);

        for (var row = 0; row < index.Count; row++)
        {
            Assert.Equal(row, index.RowAt(index.TopOf(row)));
            Assert.Equal(row, index.RowAt(index.TopOf(row + 1) - 0.25f));
        }
    }

    [Fact]
    public void SettingExtraHeightAgainReplacesItAndZeroRemovesIt()
    {
        var index = new RowOffsetIndex(10, 20f);
        index.Set(2, 40f);
        index.Set(2, 10f);
        Assert.Equal(210f, index.Total);

        index.Set(2, 0f);
        Assert.Equal(200f, index.Total);
        Assert.Equal(60f, index.TopOf(3));
    }

    [Fact]
    public void RowsOutsideTheListAndNegativeHeightsAreRejected()
    {
        var index = new RowOffsetIndex(10, 20f);

        Assert.Throws<ArgumentOutOfRangeException>(() => index.Set(10, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Set(-1, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.Set(0, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => index.TopOf(11));
    }

    [Fact]
    public void AWindowBuildsTheRowsAcrossAnExpandedRowAndPadsTheRestExactly()
    {
        var index = new RowOffsetIndex(10, 20f);
        index.Set(5, 40f);

        var window = VirtualRows.Window(130f, 50f, index, 1);

        Assert.Equal(4, window.First);
        Assert.Equal(9, window.Last);
        Assert.Equal(80f, window.PadTop);
        Assert.Equal(20f, window.PadBottom);
    }

    [Fact]
    public void AWindowPastTheEndBuildsTheLastRowsOnly()
    {
        var index = new RowOffsetIndex(10, 20f);
        index.Set(5, 40f);

        var window = VirtualRows.Window(10_000f, 50f, index, 1);

        Assert.Equal(8, window.First);
        Assert.Equal(10, window.Last);
        Assert.Equal(200f, window.PadTop);
        Assert.Equal(0f, window.PadBottom);
    }

    [Fact]
    public void AnEmptyListBuildsNothing()
    {
        var index = new RowOffsetIndex(0, 20f);

        var window = VirtualRows.Window(0f, 100f, index, 2);

        Assert.Equal(0f, index.Total);
        Assert.Equal(0, index.RowAt(5f));
        Assert.Equal(new RowWindow(0, 0, 0f, 0f), window);
    }

    [Fact]
    public void LookupsAndWindowsOverAMillionRowsAllocateNothing()
    {
        var index = new RowOffsetIndex(1_000_000, 24f);
        for (var row = 0; row < 1_000_000; row += 1000)
        {
            index.Set(row, 120f);
        }

        var sum = 0f;
        var allocated = AllocationProbe.Steady(() => sum = ScrollThrough(index));

        Assert.Equal(0L, allocated);
        Assert.True(sum > 0f);
    }

    static float ScrollThrough(RowOffsetIndex index)
    {
        var sum = 0f;
        for (var i = 0; i < 10_000; i++)
        {
            var scroll = i * 17.5f;
            var window = VirtualRows.Window(scroll, 600f, index, 4);
            sum += index.TopOf(window.First) + index.RowAt(scroll);
        }

        return sum;
    }
}

namespace Guinevere.Tests.Controls;

/// <summary>Covers how column widths are decided and which columns a viewport reaches.</summary>
public class TableLayoutTests
{
    [Fact]
    public void FixedColumnsKeepTheirWidthWithinTheirLimits()
    {
        var layout = Resolve(800f,
            Col("A", width: 120f),
            Col("B", width: 10f, minWidth: 40f),
            Col("C", width: 900f, maxWidth: 300f));

        Assert.Equal([120f, 40f, 300f], layout.Widths);
    }

    [Fact]
    public void StarColumnsShareTheFreeWidthByWeight()
    {
        var layout = Resolve(700f,
            Col("A", width: 100f),
            Col("B", width: TableLength.Star(1)),
            Col("C", width: TableLength.Star(2)));

        Assert.Equal([100f, 200f, 400f], layout.Widths);
    }

    [Fact]
    public void AStarColumnBelowItsMinimumIsPinnedThereAndTheRestShareWhatRemains()
    {
        var layout = Resolve(400f,
            Col("A", width: TableLength.Star(1), minWidth: 250f),
            Col("B", width: TableLength.Star(1)),
            Col("C", width: TableLength.Star(1)));

        Assert.Equal([250f, 75f, 75f], layout.Widths);
    }

    [Fact]
    public void AStarColumnAboveItsMaximumIsPinnedThereAndTheRestShareWhatRemains()
    {
        var layout = Resolve(600f,
            Col("A", width: TableLength.Star(1), maxWidth: 100f),
            Col("B", width: TableLength.Star(1)),
            Col("C", width: TableLength.Star(1)));

        Assert.Equal([100f, 250f, 250f], layout.Widths);
    }

    [Fact]
    public void WithNothingLeftStarColumnsTakeTheirMinimumAndTheTableScrolls()
    {
        var layout = Resolve(100f,
            Col("A", width: 90f),
            Col("B", width: TableLength.Star(1), minWidth: 60f),
            Col("C", width: TableLength.Star(3), minWidth: 80f));

        Assert.Equal([90f, 60f, 80f], layout.Widths);
        Assert.Equal(230f, layout.Total);
    }

    [Fact]
    public void AnAutoColumnTakesItsMeasuredWidthWithinItsLimits()
    {
        TableColumn[] columns =
        [
            Col("A", width: TableLength.Auto),
            Col("B", width: TableLength.Auto, maxWidth: 90f)
        ];

        var layout = TableLayout.Resolve(columns, 500f, [70f, 200f]);

        Assert.Equal([70f, 90f], layout.Widths);
    }

    [Fact]
    public void OffsetsAreCumulativeAndTheLastOneIsTheTotal()
    {
        var layout = Resolve(1000f,
            Col("A", width: 100f),
            Col("B", width: 50f),
            Col("C", width: 25f, minWidth: 10f));

        Assert.Equal([0f, 100f, 150f, 175f], layout.Offsets);
        Assert.Equal(175f, layout.Total);
    }

    [Fact]
    public void TheWindowCoversTheColumnsTheViewportTouches()
    {
        var layout = Resolve(1000f, Many(10));

        Assert.Equal((0, 3), layout.Window(0f, 250f, 0));
        Assert.Equal((2, 5), layout.Window(250f, 200f, 0));
        Assert.Equal((1, 6), layout.Window(250f, 200f, 1));
    }

    [Fact]
    public void AColumnWindowSurvivesAZeroWidthViewport()
    {
        var layout = Resolve(1000f, Col("A", width: 100f));

        Assert.Equal((0, 1), layout.Window(0f, 0f, 0));
    }

    [Fact]
    public void TheWindowNeverLeavesTheColumnList()
    {
        var layout = Resolve(1000f, Many(4));

        Assert.Equal((0, 4), layout.Window(0f, 10_000f, 5));
        Assert.Equal((0, 4), layout.Window(-50f, 100f, 5));
        Assert.Equal((3, 4), layout.Window(10_000f, 100f, 0));
    }

    [Fact]
    public void ARowWindowBracketsTheViewportAndPadsTheRest()
    {
        var window = VirtualRows.Window(scroll: 240f, viewport: 100f, stride: 24f, count: 1000, overscan: 2);

        Assert.Equal(8, window.First);
        Assert.Equal(8 + 4 + 4 + 1, window.Last);
        Assert.Equal(8 * 24f, window.PadTop);
        Assert.Equal((1000 - 17) * 24f, window.PadBottom);
    }

    [Fact]
    public void ARowWindowNeverRunsPastTheEndOfTheList()
    {
        var window = VirtualRows.Window(scroll: 0f, viewport: 10_000f, stride: 24f, count: 5, overscan: 4);

        Assert.Equal(0, window.First);
        Assert.Equal(5, window.Last);
        Assert.Equal(0f, window.PadBottom);
    }

    [Fact]
    public void ALengthIsOnlyEverBuiltFromValidInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => TableLength.Pixels(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(static () => TableLength.Star(0f));
        Assert.Equal(TableLengthKind.Pixels, ((TableLength)64f).Kind);
    }

    static TableColumn Col(string title, TableLength? width = null, float? minWidth = null, float? maxWidth = null) =>
        TableColumn<int>.Row(title, width: width, minWidth: minWidth, maxWidth: maxWidth);

    static TableColumn[] Many(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => TableColumn<int>.Row("C", id: $"c{i}", width: 100f))];

    static TableLayout Resolve(float viewport, params TableColumn[] columns) =>
        TableLayout.Resolve(columns, viewport, new float[columns.Length]);
}

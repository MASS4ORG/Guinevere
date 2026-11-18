namespace Guinevere.Tests.Controls;

/// <summary>Covers how column widths are decided and which columns a viewport reaches.</summary>
public class TableLayoutTests
{
    [Fact]
    public void FixedColumnsKeepTheirWidthWithinTheirLimits()
    {
        var layout = Resolve(800f,
            new TableColumn("A", new("a")) { Width = 120f },
            new TableColumn("B", new("b")) { Width = 10f, MinWidth = 40f },
            new TableColumn("C", new("c")) { Width = 900f, MaxWidth = 300f });

        Assert.Equal([120f, 40f, 300f], layout.Widths);
    }

    [Fact]
    public void StarColumnsShareTheFreeWidthByWeight()
    {
        var layout = Resolve(700f,
            new TableColumn("A", new("a")) { Width = 100f },
            new TableColumn("B", new("b")) { Width = TableLength.Star(1) },
            new TableColumn("C", new("c")) { Width = TableLength.Star(2) });

        Assert.Equal([100f, 200f, 400f], layout.Widths);
    }

    [Fact]
    public void AStarColumnBelowItsMinimumIsPinnedThereAndTheRestShareWhatRemains()
    {
        var layout = Resolve(400f,
            new TableColumn("A", new("a")) { Width = TableLength.Star(1), MinWidth = 250f },
            new TableColumn("B", new("b")) { Width = TableLength.Star(1) },
            new TableColumn("C", new("c")) { Width = TableLength.Star(1) });

        Assert.Equal([250f, 75f, 75f], layout.Widths);
    }

    [Fact]
    public void AStarColumnAboveItsMaximumIsPinnedThereAndTheRestShareWhatRemains()
    {
        var layout = Resolve(600f,
            new TableColumn("A", new("a")) { Width = TableLength.Star(1), MaxWidth = 100f },
            new TableColumn("B", new("b")) { Width = TableLength.Star(1) },
            new TableColumn("C", new("c")) { Width = TableLength.Star(1) });

        Assert.Equal([100f, 250f, 250f], layout.Widths);
    }

    [Fact]
    public void WithNothingLeftStarColumnsTakeTheirMinimumAndTheTableScrolls()
    {
        var layout = Resolve(100f,
            new TableColumn("A", new("a")) { Width = 90f },
            new TableColumn("B", new("b")) { Width = TableLength.Star(1), MinWidth = 60f },
            new TableColumn("C", new("c")) { Width = TableLength.Star(3), MinWidth = 80f });

        Assert.Equal([90f, 60f, 80f], layout.Widths);
        Assert.Equal(230f, layout.Total);
    }

    [Fact]
    public void AnAutoColumnTakesItsMeasuredWidthWithinItsLimits()
    {
        var columns = new[]
        {
            new TableColumn("A", new("a")) { Width = TableLength.Auto },
            new TableColumn("B", new("b")) { Width = TableLength.Auto, MaxWidth = 90f }
        };

        var layout = TableLayout.Resolve(columns, 500f, [70f, 200f]);

        Assert.Equal([70f, 90f], layout.Widths);
    }

    [Fact]
    public void OffsetsAreCumulativeAndTheLastOneIsTheTotal()
    {
        var layout = Resolve(1000f,
            new TableColumn("A", new("a")) { Width = 100f },
            new TableColumn("B", new("b")) { Width = 50f },
            new TableColumn("C", new("c")) { Width = 25f, MinWidth = 10f });

        Assert.Equal([0f, 100f, 150f, 175f], layout.Offsets);
        Assert.Equal(175f, layout.Total);
    }

    [Fact]
    public void TheWindowCoversTheColumnsTheViewportTouches()
    {
        var layout = Resolve(1000f, [.. Enumerable.Range(0, 10)
            .Select(static i => new TableColumn("C", new($"c{i}")) { Width = 100f })]);

        Assert.Equal((0, 3), layout.Window(0f, 250f, 0));
        Assert.Equal((2, 5), layout.Window(250f, 200f, 0));
        Assert.Equal((1, 6), layout.Window(250f, 200f, 1));
    }

    [Fact]
    public void AColumnWindowSurvivesAZeroWidthViewport()
    {
        var layout = Resolve(1000f, new TableColumn("A", new("a")) { Width = 100f });

        Assert.Equal((0, 1), layout.Window(0f, 0f, 0));
    }

    [Fact]
    public void TheWindowNeverLeavesTheColumnList()
    {
        var layout = Resolve(1000f, [.. Enumerable.Range(0, 4)
            .Select(static i => new TableColumn("C", new($"c{i}")) { Width = 100f })]);

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

    static TableLayout Resolve(float viewport, params TableColumn[] columns) =>
        TableLayout.Resolve(columns, viewport, new float[columns.Length]);
}

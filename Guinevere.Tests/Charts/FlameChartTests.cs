using Guinevere.Charts;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Charts;

/// <summary>Checks depth calculation and virtualized flame drawing through chart bounds.</summary>
public sealed class FlameChartTests
{
    /// <summary>Parent links may be reversed without overflowing the call stack.</summary>
    [Fact]
    public void ResolvesDeepChainsWithoutRecursion()
    {
        var parents = new int[50_000];
        var depths = new int[parents.Length];
        for (var i = 0; i < parents.Length - 1; i++)
        {
            parents[i] = i + 1;
        }
        parents[^1] = -1;
        Assert.True(FlameLayout.TryCalculateDepths(parents, depths));
        Assert.Equal(parents.Length - 1, depths[0]);
        Assert.Equal(0, depths[^1]);
        Assert.True(FlameLayout.TryCalculateDepths([-1, 0, 1, -1, 0], depths));
        Assert.Equal(new[] { 0, 1, 2, 0, 1 }, depths[..5]);
        Assert.False(FlameLayout.TryCalculateDepths([1, 0], depths));
        Assert.False(FlameLayout.TryCalculateDepths([9], depths));
        Assert.False(FlameLayout.TryCalculateDepths(parents, parents));
        Assert.False(FlameLayout.TryCalculateDepths(parents, depths.AsSpan(0, 1)));
    }

    /// <summary>Search and breadcrumbs retain source indices without partial paths.</summary>
    [Fact]
    public void FindsNamedScopesAndWritesRootToLeafPaths()
    {
        Span<int> path = stackalloc int[3];
        Assert.Equal(3, FlameLayout.WritePath([2, -1, 1, -1], selected: 0, path));
        Assert.Equal(new[] { 1, 2, 0 }, path.ToArray());
        Assert.Equal(3, FlameLayout.WritePath([2, -1, 1, -1], selected: 0, path[..2]));
        Assert.Equal(0, FlameLayout.WritePath([0], selected: 0, path));
        Assert.Equal(0, FlameLayout.WritePath([-1, -1], selected: 2, path));

        string[] labels = ["Paint", "Display", "Paint child"];
        Assert.Equal(2, FlameLayout.Search(labels, "pAInt", path));
        Assert.Equal(new[] { 0, 2 }, path[..2].ToArray());
        Assert.Equal(2, FlameLayout.Search(labels, "paint", path[..1]));
        Assert.Equal(0, FlameLayout.Search(labels, "", path));
    }

    /// <summary>Only the visible time and lane ranges emit commands and the hit retains its source index.</summary>
    [Fact]
    public void DrawCullsOutsideVisibleTimeAndRows()
    {
        using var harness = new FrameHarness(140, 100);
        ReadOnlyMemory<FlameInterval>[] rows =
        [
            new FlameInterval[] { new(2, 3, 0) },
            new FlameInterval[] { new(0, 1, 1), new(2, 2.01, 4), new(10, 11, 5) },
            new FlameInterval[] { new(2, 3, 6) },
        ];
        var plot = new TimelinePlot(new Rect(5, 5, 100, 24), new ChartRange(1, 6), 24);
        var commandCount = 0;
        harness.Frame(gui =>
        {
            FlameChart.Draw(gui, plot, rows, [Color.Red, Color.Blue], firstRow: 1, selected: 4,
                selectionColor: Color.White);
            commandCount = gui.CurrentNode.DrawList.PrimitiveCount;
        });
        Assert.Equal(2, commandCount);
        Assert.Equal(4, FlameChart.HitTest(plot, rows, new Vector2(27, 12), firstRow: 1));
        Assert.Equal(-1, FlameChart.HitTest(plot, rows, new Vector2(33, 12), firstRow: 1));
        Assert.Equal(-1, FlameChart.HitTest(plot, rows, new Vector2(26, 28), firstRow: 1));
    }
}

using System.Text.RegularExpressions;
using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the table control: that a huge source only builds the cells in view, that the header stays
/// put and follows the body sideways, and that empty and loading sources say so.
/// </summary>
public class TableTests
{
    const int Width = 400;
    const int Height = 400;

    static readonly TableTheme Theme = new() { RowHeight = 24f, HeaderHeight = 28f };

    [Fact]
    public void AMillionRowSourceOnlyBuildsTheRowsOnScreen()
    {
        var source = new GridSource(1_000_000, 3);
        var gui = RunFrames(source, Columns(3), frames: 2);

        Assert.InRange(RowIndices(gui).Count, 1, 40);
    }

    [Fact]
    public void ARowCountIsAllTheTableAsksForUpFront()
    {
        var source = new GridSource(1_000_000, 3);
        var gui = RunFrames(source, Columns(3), frames: 2);

        source.Reads = 0;
        RunFrames(source, Columns(3), frames: 1, reuse: gui);

        // Cells in view, in both passes; never a pass over the dataset.
        Assert.InRange(source.Reads, 1, 40 * 3 * 2);
    }

    [Fact]
    public void TheSpacersKeepTheScrollExtentOfTheWholeSource()
    {
        var gui = RunFrames(new GridSource(1000, 2), Columns(2), frames: 2);

        var bottom = gui.RootNode!.FindChildById("table/padBottom");
        Assert.NotNull(bottom);
        Assert.True(bottom.Rect.H > 0, "the rows below the viewport must still take space");
    }

    [Fact]
    public void ScrollingKeepsTheVisibleRowsBuilt()
    {
        var source = new GridSource(10_000, 2);
        var state = new TableState();
        var gui = RunFrames(source, Columns(2), state);

        gui.ScrollBy("table/body", new Vector2(0, 24 * 5000));
        gui = RunFrames(source, Columns(2), state, frames: 2, reuse: gui);

        var rows = RowIndices(gui);
        Assert.NotEmpty(rows);
        Assert.True(rows.Min() <= 5000 && rows.Max() >= 5000,
            $"window {rows.Min()}..{rows.Max()} does not bracket row 5000");
    }

    [Fact]
    public void TheHeaderStaysAtTheTopWhileRowsScroll()
    {
        var source = new GridSource(10_000, 2);
        var state = new TableState();
        var gui = RunFrames(source, Columns(2), state);
        var top = gui.RootNode!.FindChildById("table/header")!.Rect.Y;

        gui.ScrollBy("table/body", new Vector2(0, 24 * 5000));
        gui = RunFrames(source, Columns(2), state, frames: 2, reuse: gui);

        Assert.Equal(top, gui.RootNode!.FindChildById("table/header")!.Rect.Y);
    }

    [Fact]
    public void TheHeaderFollowsTheBodySideways()
    {
        var columns = Fixed(5, 300f);
        var source = new GridSource(20, 5);
        var state = new TableState();
        var gui = RunFrames(source, columns, state, frames: 2);

        gui.ScrollBy("table/body", new Vector2(250, 0));
        gui = RunFrames(source, columns, state, frames: 2, reuse: gui);

        var header = gui.RootNode!.FindChildById("table/header/c1")!.Rect.X;
        var cell = gui.RootNode!.FindChildById("table/row0/c1")!.Rect.X;
        Assert.Equal(cell, header, 0.5f);
        Assert.True(header < 300f, "the column must have scrolled left under the header");
    }

    [Fact]
    public void StarColumnsThatFitShowNoHorizontalScrollbar()
    {
        var gui = RunFrames(new GridSource(5, 3), Columns(3), frames: 4);

        var scroll = gui.GetScrollState(gui.RootNode!.FindChildById("table/body")!.Id);
        Assert.NotNull(scroll);
        Assert.False(scroll.NeedsHorizontalScroll);
    }

    [Fact]
    public void VirtualizedColumnsOnlyBuildTheColumnsInView()
    {
        var columns = Fixed(200, 100f);
        var source = new GridSource(10, 200);

        var gui = RunFrames(source, columns, options: new TableOptions { VirtualizeColumns = true }, frames: 3);
        var all = RunFrames(source, columns, frames: 3);

        var virtualized = CellIds(gui, row: 0).Count;
        Assert.InRange(virtualized, 1, 6);
        Assert.Equal(200, CellIds(all, row: 0).Count);
    }

    [Fact]
    public void VirtualizedColumnsStillCoverAScrolledViewport()
    {
        var columns = Fixed(200, 100f);
        var source = new GridSource(10, 200);
        var options = new TableOptions { VirtualizeColumns = true };
        var state = new TableState();
        var gui = RunFrames(source, columns, state, options, frames: 2);

        gui.ScrollBy("table/body", new Vector2(10_000, 0));
        gui = RunFrames(source, columns, state, options, frames: 3, reuse: gui);

        var built = CellIds(gui, row: 0);
        Assert.Contains("c100", built);
        Assert.DoesNotContain("c0", built);
    }

    [Fact]
    public void ACustomRendererDrawsOnlyTheCellsInView()
    {
        var renderer = new RecordingRenderer();
        TableColumn[] columns =
        [
            new("Name", new("name")) { Width = 200f },
            new("Size", new("size")) { Width = 100f, Renderer = renderer }
        ];
        var source = new GridSource(100_000, 2);
        var state = new TableState();
        var gui = RunFrames(source, columns, state, frames: 2);

        Assert.NotEmpty(renderer.Cells);
        Assert.All(renderer.Cells, cell =>
        {
            Assert.Equal(new TableColumnId("size"), cell.Column.Id);
            Assert.Equal(100f, cell.Width);
            Assert.Equal(Theme.RowHeight, cell.Height);
            Assert.Equal($"r{cell.Row}/size", cell.Value);
            Assert.Same(source, cell.Source);
            Assert.Same(state, cell.State);
        });
        Assert.True(renderer.Cells.Max(static cell => cell.Row) < 60);
        Assert.NotNull(gui);
    }

    [Fact]
    public void ARendererCanReachTheStableIdentityOfItsRow()
    {
        var renderer = new RecordingRenderer();
        TableColumn[] columns = [new("Size", new("size")) { Width = 100f, Renderer = renderer }];
        RunFrames(new GridSource(50, 1), columns, frames: 2);

        Assert.All(renderer.Cells, static cell => Assert.Equal(RowIdentity.FromIndex(cell.Row), cell.Identity));
    }

    [Fact]
    public void AnEmptySourceShowsTheEmptyState()
    {
        var gui = RunFrames(new GridSource(0, 2), Columns(2), frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/table-empty"));
        Assert.Null(gui.RootNode!.FindChildById("table/table-loading"));
        Assert.Empty(RowIndices(gui));
    }

    [Fact]
    public void ALoadingEmptySourceShowsTheLoadingStateInsteadOfTheEmptyOne()
    {
        var gui = RunFrames(new GridSource(0, 2) { Loading = true }, Columns(2), frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/table-loading"));
        Assert.Null(gui.RootNode!.FindChildById("table/table-empty"));
    }

    [Fact]
    public void ALoadingSourceKeepsItsRowsAndAddsALoadingRow()
    {
        var gui = RunFrames(new GridSource(5, 2) { Loading = true }, Columns(2), frames: 2);

        Assert.Equal(5, RowIndices(gui).Count);
        Assert.NotNull(gui.RootNode!.FindChildById("table/table-loading"));
    }

    [Fact]
    public void TheSortedColumnGetsAMarkerAndTheOthersDoNot()
    {
        var state = new TableState { Sort = new TableSort(new TableColumnId("c1"), TableSortDirection.Descending) };
        var gui = RunFrames(new GridSource(3, 3), Columns(3), state, frames: 2);

        Assert.Equal(2, gui.RootNode!.FindChildById("table/header/c1")!.Children.Count);
        Assert.Single(gui.RootNode!.FindChildById("table/header/c0")!.Children);
    }

    [Fact]
    public void AColumnThatIsNotInitiallyVisibleIsLeftOut()
    {
        TableColumn[] columns = [new("A", new("a")), new("B", new("b")) { InitiallyVisible = false }];
        var gui = RunFrames(new GridSource(3, 2), columns, frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/header/a"));
        Assert.Null(gui.RootNode!.FindChildById("table/header/b"));
    }

    [Fact]
    public void ARightAlignedCellSitsAtItsTrailingEdge()
    {
        TableColumn[] columns = [new("N", new("n")) { Width = 200f, Alignment = TableAlignment.Right }];
        var gui = RunFrames(new GridSource(1, 1), columns, frames: 2);

        var cell = gui.RootNode!.FindChildById("table/row0/n")!;
        var text = cell.Children[0];
        Assert.Equal(cell.Rect.X + cell.Rect.W - Theme.CellPadding, text.Rect.X + text.Rect.W, 1f);
    }

    [Fact]
    public void ADuplicateColumnIdIsRejected()
    {
        TableColumn[] columns = [new("A", new("a")), new("Again", new("a"))];

        Assert.Throws<ArgumentException>(() => RunFrames(new GridSource(1, 1), columns));
    }

    [Fact]
    public void TheTableIsDrawnWithoutAnySheetRulesOfItsOwn()
    {
        var gui = RunFrames(new GridSource(2, 2), Columns(2));

        Assert.NotNull(gui.RootNode);
    }

    static TableColumn[] Columns(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => new TableColumn($"Column {i}", new($"c{i}")))];

    static TableColumn[] Fixed(int count, float width) =>
        [.. Enumerable.Range(0, count).Select(i => new TableColumn($"Column {i}", new($"c{i}")) { Width = width })];

    static Gui RunFrames(GridSource source, IReadOnlyList<TableColumn> columns, TableState? state = null,
        TableOptions? options = null, int frames = 1, Gui? reuse = null)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        state ??= new TableState();

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        var gui = reuse ?? new TestableGui { Input = input };
        gui.Input = input;
        if (gui is TestableGui testable)
        {
            testable.SetScreenRect(Width, Height);
        }

        for (var frame = 0; frame < frames; frame++)
        {
            void Draw() => gui.Table(state, source, columns, options, Theme);

            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            Draw();
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            Draw();
            gui.Render();
            gui.EndFrame();
        }

        return gui;
    }

    static List<int> RowIndices(Gui gui)
    {
        var indices = new List<int>();
        Visit(gui.RootNode!);
        return indices;

        void Visit(LayoutNode node)
        {
            var match = Regex.Match(node.Id, @"^table/row(\d+)$");
            if (match.Success)
            {
                indices.Add(int.Parse(match.Groups[1].Value));
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }
    }

    /// <summary>The column ids of the cells built in one row.</summary>
    static List<string> CellIds(Gui gui, int row)
    {
        var ids = new List<string>();
        var prefix = $"table/row{row}/";
        Visit(gui.RootNode!);
        return ids;

        void Visit(LayoutNode node)
        {
            // Cell nodes only: a cell's own children extend its id, and the spacers are not cells.
            if (node.Id.StartsWith(prefix, StringComparison.Ordinal) && !node.Id[prefix.Length..].Contains('/')
                && !node.Id.EndsWith("padLeft", StringComparison.Ordinal)
                && !node.Id.EndsWith("padRight", StringComparison.Ordinal))
            {
                ids.Add(node.Id[prefix.Length..]);
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }
    }

    /// <summary>A source of <c>r{row}/{column}</c> strings that counts how often it is read.</summary>
    sealed class GridSource(int rows, int columns) : ITableViewSource
    {
        public int Reads { get; set; }

        public bool Loading { get; init; }

        public int RowCount => rows;

        public bool IsLoading => Loading;

        public int ColumnCount => columns;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public object? GetValue(int row, TableColumn column)
        {
            Reads++;
            return $"r{row}/{column.Id.Value}";
        }

        public RowIdentity GetRowIdentity(int row) => RowIdentity.FromIndex(row);
    }

    sealed class RecordingRenderer : ITableCellRenderer
    {
        public List<TableCellContext> Cells { get; } = [];

        public void Render(Gui gui, in TableCellContext cell)
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                Cells.Add(cell);
            }
        }
    }
}

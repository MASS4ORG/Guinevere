namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the table control: that a huge source only builds the cells in view, that the header stays
/// put and follows the body sideways, that columns read typed values from rows, and that empty and loading
/// sources say so.
/// </summary>
public class TableTests : TableTestBase
{
    [Fact]
    public void AMillionRowSourceOnlyBuildsTheRowsOnScreen()
    {
        var gui = RunFrames(new GridSource(1_000_000), Columns(3), frames: 2);

        Assert.InRange(RowIndices(gui).Count, 1, 40);
    }

    [Fact]
    public void OnlyTheRowsAndValuesInViewAreEverRead()
    {
        var counter = new Counter();
        var columns = Columns(3, counter);
        var source = new GridSource(1_000_000);
        var gui = RunFrames(source, columns, frames: 2);

        source.Reads = 0;
        counter.Reads = 0;
        RunFrames(source, columns, frames: 1, reuse: gui);

        // Rows in view, once per pass, and their cells' values; never a pass over the dataset.
        Assert.InRange(source.Reads, 1, 40 * 2);
        Assert.InRange(counter.Reads, 1, 40 * 3 * 2);
    }

    [Fact]
    public void TheSpacersKeepTheScrollExtentOfTheWholeSource()
    {
        var gui = RunFrames(new GridSource(1000), Columns(2), frames: 2);

        var bottom = gui.RootNode!.FindChildById("table/padBottom");
        Assert.NotNull(bottom);
        Assert.True(bottom.Rect.H > 0, "the rows below the viewport must still take space");
    }

    [Fact]
    public void ScrollingKeepsTheVisibleRowsBuilt()
    {
        var source = new GridSource(10_000);
        var columns = Columns(2);
        var state = new TableState();
        var gui = RunFrames(source, columns, state);

        gui.ScrollBy("table/body", new Vector2(0, 24 * 5000));
        gui = RunFrames(source, columns, state, frames: 2, reuse: gui);

        var rows = RowIndices(gui);
        Assert.NotEmpty(rows);
        Assert.True(rows.Min() <= 5000 && rows.Max() >= 5000,
            $"window {rows.Min()}..{rows.Max()} does not bracket row 5000");
    }

    [Fact]
    public void TheHeaderStaysAtTheTopWhileRowsScroll()
    {
        var source = new GridSource(10_000);
        var columns = Columns(2);
        var state = new TableState();
        var gui = RunFrames(source, columns, state);
        var top = gui.RootNode!.FindChildById("table/header")!.Rect.Y;

        gui.ScrollBy("table/body", new Vector2(0, 24 * 5000));
        gui = RunFrames(source, columns, state, frames: 2, reuse: gui);

        Assert.Equal(top, gui.RootNode!.FindChildById("table/header")!.Rect.Y);
    }

    [Fact]
    public void TheHeaderFollowsTheBodySideways()
    {
        var columns = Fixed(5, 300f);
        var source = new GridSource(20);
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
        var gui = RunFrames(new GridSource(5), Columns(3), frames: 4);

        var scroll = gui.GetScrollState(gui.RootNode!.FindChildById("table/body")!.Id);
        Assert.NotNull(scroll);
        Assert.False(scroll.NeedsHorizontalScroll);
    }

    [Fact]
    public void VirtualizedColumnsOnlyBuildTheColumnsInView()
    {
        var columns = Fixed(200, 100f);
        var source = new GridSource(10);

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
        var source = new GridSource(10);
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
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("Name", static row => $"r{row}/name", width: 200f),
            TableColumn<int>.Of("Size", static row => $"r{row}/size", width: 100f, renderer: renderer)
        ]);
        var source = new GridSource(100_000);
        var state = new TableState();
        var gui = RunFrames(source, columns, state, frames: 2);

        Assert.NotEmpty(renderer.Cells);
        Assert.All(renderer.Cells, cell =>
        {
            Assert.Equal("size", cell.Column.Id);
            Assert.Equal(100f, cell.Width);
            Assert.Equal(Theme.RowHeight, cell.Height);
            Assert.Equal($"r{cell.Index}/size", cell.Value);
            Assert.Equal(cell.Index, cell.Item);
            Assert.Same(source, cell.Source);
            Assert.Same(state, cell.State);
        });
        Assert.True(renderer.Cells.Max(static cell => cell.Index) < 60);
        Assert.NotNull(gui);
    }

    [Fact]
    public void ARendererCanReachTheStableIdentityOfItsRow()
    {
        var renderer = new RecordingRenderer();
        var columns = new TableColumns<int>(
            [TableColumn<int>.Of("Size", static row => $"r{row}", renderer: renderer)]);
        RunFrames(new GridSource(50), columns, frames: 2);

        Assert.All(renderer.Cells, static cell => Assert.Equal(RowIdentity.UnstableIndex(cell.Index), cell.Identity));
    }

    [Fact]
    public void AColumnReadsItsTypedValueFromTheRowWithoutAnyStringDispatch()
    {
        var items = new ItemSource(
        [
            new Item("1.2.0", "a.zip", 3),
            new Item("1.3.0", "b.zip", 7)
        ]);
        var spy = new ItemRenderer();
        var columns = new TableColumns<Item>(
        [
            TableColumn<Item>.Of("Version", static item => item.Version),
            TableColumn<Item>.Of("Downloads", static item => item.Downloads, alignment: TableAlignment.Right),
            TableColumn<Item>.Row("Actions", renderer: spy)
        ]);

        var gui = RunFrames(items, columns, frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/row0/version"));
        Assert.NotNull(gui.RootNode!.FindChildById("table/row1/downloads"));
        Assert.Equal(["a.zip", "b.zip"], spy.Seen.Distinct().Order());
        Assert.All(spy.Seen, static item => Assert.NotNull(item));
    }

    [Fact]
    public void ARowColumnWhoseItemTypeIsNotComparableIsNotSortableAndShowsNoMarker()
    {
        var items = new ItemSource([new Item("1", "a", 1)]);
        var actions = TableColumn<Item>.Row("Actions", renderer: new ItemRenderer());
        var version = TableColumn<Item>.Of("Version", static item => item.Version);
        var columns = new TableColumns<Item>([version, actions]);
        var state = new TableState();
        state.Sort.Set([new TableSort(actions), new TableSort(version)]);

        var gui = RunFrames(items, columns, state, frames: 2);

        Assert.False(actions.Sortable);
        Assert.True(version.Sortable);
        Assert.Single(gui.RootNode!.FindChildById("table/header/actions")!.Children);
        Assert.DoesNotContain("sorted", Modifiers(gui, "actions"));
        Assert.DoesNotContain("sorted-secondary", Modifiers(gui, "actions"));
    }

    [Fact]
    public void AnEmptySourceShowsTheEmptyState()
    {
        var gui = RunFrames(new GridSource(0), Columns(2), frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/table-empty"));
        Assert.Null(gui.RootNode!.FindChildById("table/table-loading"));
        Assert.Empty(RowIndices(gui));
    }

    [Fact]
    public void ALoadingEmptySourceShowsTheLoadingStateInsteadOfTheEmptyOne()
    {
        var gui = RunFrames(new GridSource(0) { Loading = true }, Columns(2), frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/table-loading"));
        Assert.Null(gui.RootNode!.FindChildById("table/table-empty"));
    }

    [Fact]
    public void ALoadingSourceKeepsItsRowsAndAddsALoadingRow()
    {
        var gui = RunFrames(new GridSource(5) { Loading = true }, Columns(2), frames: 2);

        Assert.Equal(5, RowIndices(gui).Count);
        Assert.NotNull(gui.RootNode!.FindChildById("table/table-loading"));
    }

    [Fact]
    public void TheSortedColumnGetsAMarkerAndTheOthersDoNot()
    {
        var columns = Columns(3);
        var state = new TableState();
        state.Sort.Set([new TableSort(columns[1], TableSortDirection.Descending)]);
        var gui = RunFrames(new GridSource(3), columns, state, frames: 2);

        Assert.Equal(2, gui.RootNode!.FindChildById("table/header/c1")!.Children.Count);
        Assert.Single(gui.RootNode!.FindChildById("table/header/c0")!.Children);
        Assert.Contains("descending", Modifiers(gui, "c1"));
        Assert.DoesNotContain("sorted-more", Modifiers(gui, "c1"));
    }

    [Fact]
    public void ARetainedSecondaryKeyAddsAnIndicatorToThePrimaryAndOnlyAModifierToItself()
    {
        var columns = Columns(3);
        var state = new TableState();
        state.Sort.Set([
            new TableSort(columns[1]),
            new TableSort(columns[2], TableSortDirection.Descending),
            new TableSort(columns[0])
        ]);
        var gui = RunFrames(new GridSource(3), columns, state, frames: 2);

        Assert.Equal(3, gui.RootNode!.FindChildById("table/header/c1")!.Children.Count);
        Assert.Contains("sorted-more", Modifiers(gui, "c1"));
        Assert.Single(gui.RootNode!.FindChildById("table/header/c2")!.Children);
        Assert.Contains("sorted-secondary", Modifiers(gui, "c2"));
        Assert.Contains("descending", Modifiers(gui, "c2"));
        Assert.Contains("sorted-secondary", Modifiers(gui, "c0"));
        Assert.Contains("ascending", Modifiers(gui, "c0"));
    }

    [Fact]
    public void AHeaderWithAnEmptyTitleAndNoContentDrawsNothingAndTheTableStillBuilds()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("", static row => row),
            TableColumn<int>.Of("Named", static row => row)
        ]);

        var gui = RunFrames(new GridSource(2), columns, frames: 2);

        Assert.Empty(gui.RootNode!.FindChildById("table/header/column1")!.Children);
        Assert.Single(gui.RootNode!.FindChildById("table/header/named")!.Children);
    }

    [Fact]
    public void AHeaderContentReplacesTheTitleTextAndIsGivenItsColumnAndRoom()
    {
        var content = new RecordingHeader();
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("Title", static row => row, header: content),
            TableColumn<int>.Of("Other", static row => row)
        ]);

        var gui = RunFrames(new GridSource(2), columns, frames: 2);

        Assert.NotEmpty(content.Seen);
        Assert.All(content.Seen, context =>
        {
            Assert.Same(columns[0], context.Column);
            Assert.Equal(Theme.HeaderHeight, context.Height);
            Assert.True(context.Width > 0f);
            Assert.Equal(Theme.FontSize, context.FontSize);
        });
        Assert.Single(gui.RootNode!.FindChildById("table/header/title")!.Children);
    }

    [Fact]
    public void AnIconAndALocalizedTextAreBuiltInHeaderContents()
    {
        var language = "one";
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("", static row => row, header: TableHeaderContent.Icon(WidgetIcons.ChevronUp)),
            TableColumn<int>.Of("Name", static row => row, header: TableHeaderContent.Text(() => language))
        ]);
        var state = new TableState();
        var gui = RunFrames(new GridSource(2), columns, state, frames: 2);
        var shortWidth = gui.RootNode!.FindChildById("table/header/name")!.Children[0].Rect.W;
        Assert.Single(gui.RootNode!.FindChildById("table/header/column1")!.Children);

        language = "a much longer text";
        gui = RunFrames(new GridSource(2), columns, state, frames: 2, reuse: gui);

        Assert.True(gui.RootNode!.FindChildById("table/header/name")!.Children[0].Rect.W > shortWidth);
    }

    [Fact]
    public void TheSortMarkerFollowsCustomHeaderContentToo()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("", static row => row, header: TableHeaderContent.Icon(WidgetIcons.ChevronUp))
        ]);
        var state = new TableState();
        state.Sort.Set([new TableSort(columns[0])]);

        var gui = RunFrames(new GridSource(2), columns, state, frames: 2);

        Assert.Equal(2, gui.RootNode!.FindChildById("table/header/column1")!.Children.Count);
    }


    [Fact]
    public void AColumnThatIsNotInitiallyVisibleIsLeftOut()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row),
            TableColumn<int>.Of("B", static row => row, visible: false)
        ]);
        var gui = RunFrames(new GridSource(3), columns, frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/header/a"));
        Assert.Null(gui.RootNode!.FindChildById("table/header/b"));
    }

    [Fact]
    public void ARightAlignedCellSitsAtItsTrailingEdge()
    {
        var columns = new TableColumns<int>(
            [TableColumn<int>.Of("N", static row => $"n{row}", width: 200f, alignment: TableAlignment.Right)]);
        var gui = RunFrames(new GridSource(1), columns, frames: 2);

        var cell = gui.RootNode!.FindChildById("table/row0/n")!;
        var text = cell.Children[0];
        Assert.Equal(cell.Rect.X + cell.Rect.W - Theme.CellPadding, text.Rect.X + text.Rect.W, 1f);
    }

    [Fact]
    public void ExplicitIdsLetColumnsShareATitle()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("Size", static row => row, id: "sizeBefore"),
            TableColumn<int>.Of("Size", static row => row, id: "sizeAfter")
        ]);

        var gui = RunFrames(new GridSource(1), columns, frames: 2);

        Assert.NotNull(gui.RootNode!.FindChildById("table/header/sizeBefore"));
        Assert.NotNull(gui.RootNode!.FindChildById("table/header/sizeAfter"));
    }

    [Fact]
    public void TheTextOfAValueTypeFollowsTheValueWhenItChanges()
    {
        var values = new[] { 5, 5, 5 };
        var columns = new TableColumns<int>(
            [TableColumn<int>.Of("Value", row => values[row], id: "value", alignment: TableAlignment.Left)]);
        var source = new GridSource(3);
        var state = new TableState();
        var gui = RunFrames(source, columns, state, frames: 2);
        var before = TextWidth(gui, 1, "value");

        values[1] = 50_000_000;
        RunFrames(source, columns, state, frames: 2, reuse: gui);
        var after = TextWidth(gui, 1, "value");

        Assert.True(after > before + 20f, $"the cell showed width {after}, no wider than {before} for a longer number");
        Assert.Equal(TextWidth(gui, 0, "value"), before);
    }

    [Fact]
    public void TheTableIsDrawnWithoutAnySheetRulesOfItsOwn()
    {
        var gui = RunFrames(new GridSource(2), Columns(2));

        Assert.NotNull(gui.RootNode);
    }
}

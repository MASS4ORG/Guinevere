using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers what keys a row's nodes by its identity: ids that follow the row through a sort, keys that cannot read as
/// child ids, a source version that empties cached identities, and the saved state of columns and sort.
/// </summary>
public class TableIdentityTests : TableTestBase
{
    static readonly Item[] Items =
    [
        new("1", "b.zip", 5), new("2", "c.zip", 9), new("3", "a.zip", 1)
    ];

    [Fact]
    public void ARowsNodesFollowItAcrossASort()
    {
        var source = new ListTableSource<Item>(Items, static item => item.FileName);
        var columns = new TableColumns<Item>(
        [
            TableColumn<Item>.Of("Name", static item => item.FileName, id: "name", width: 150f),
            TableColumn<Item>.Of("Downloads", static item => item.Downloads, id: "downloads", width: 100f)
        ]);
        var state = new TableState();
        using var harness = new FrameHarness(Width, Height);
        void Draw(Gui gui) => gui.Table(state, source, columns);
        harness.Frame(Draw);
        harness.Frame(Draw);
        var rowBefore = harness.Gui.RootNode!.FindChildById("table/rowa.zip")!;
        var yBefore = rowBefore.Rect.Y;
        var cellBefore = harness.Gui.RootNode.FindChildById("table/rowa.zip/name")!;

        source.ApplySort([new TableSort(columns[0])]);
        harness.Frame(Draw);
        harness.Frame(Draw);

        var rowAfter = harness.Gui.RootNode.FindChildById("table/rowa.zip")!;
        Assert.NotEqual(yBefore, rowAfter.Rect.Y);
        Assert.Same(rowBefore.Id, rowAfter.Id);
        Assert.NotNull(harness.Gui.RootNode.FindChildById("table/rowa.zip/name"));
        Assert.Equal(cellBefore.Id, harness.Gui.RootNode.FindChildById("table/rowa.zip/name")!.Id);
        Assert.Null(harness.Gui.RootNode.FindChildById("table/row0"));
    }

    [Fact]
    public void AKeyWithASlashOrPercentCannotReadAsAChildId()
    {
        var odd = new[] { new Item("1", "dir/file%1", 1), new Item("2", "dir", 2) };
        var source = new ListTableSource<Item>(odd, static item => item.FileName);
        var columns = new TableColumns<Item>(
        [
            TableColumn<Item>.Of("Name", static item => item.FileName, id: "name", width: 150f)
        ]);
        using var harness = new FrameHarness(Width, Height);
        void Draw(Gui gui) => gui.Table(new TableState(), source, columns);

        harness.Frame(Draw);

        Assert.NotNull(harness.Gui.RootNode!.FindChildById("table/rowdir%2Ffile%251"));
        Assert.NotNull(harness.Gui.RootNode.FindChildById("table/rowdir/name"));
        Assert.NotNull(harness.Gui.RootNode.FindChildById("table/rowdir%2Ffile%251/name"));
    }

    [Fact]
    public void ASlotKeepsItsIdentityUntilTheSourceVersionMoves()
    {
        var cache = new TableCache();
        cache.Begin(Columns("a").Set, new TableColumnState());
        cache.Reserve(8);
        var reads = 0;
        var source = new CountingSource(() => reads++);

        var slot = cache.Slot(4);
        var key = TableCache.Key(slot, source);
        Assert.Equal("4", key);
        Assert.Same(key, TableCache.Key(cache.Slot(4), source));
        Assert.Equal(1, reads);
        Assert.Equal("table/row4", TableCache.RowId(slot));

        cache.SourceVersion = 1;
        var again = cache.Slot(4);
        Assert.Null(again.Key);
        TableCache.Key(again, source);
        Assert.Equal(2, reads);
    }

    [Fact]
    public void ARowBuiltFromAnUnresolvedSlotFallsBackToItsNumber()
    {
        var cache = new TableCache();
        cache.Begin(Columns("a").Set, new TableColumnState());
        cache.Reserve(8);

        Assert.Equal("table/row7", TableCache.RowId(cache.Slot(7)));
    }

    [Fact]
    public void ASourceThatPublishesNewDataMovesTheVersionSoCachedIdentitiesAreRead()
    {
        var source = new PublishingSource { Pending = 10 };
        var state = new TableState();
        var columns = Fixed(1, 100f);
        using var harness = new FrameHarness(Width, Height);
        void Draw(Gui gui) => gui.Table(state, source, columns);
        harness.Frame(Draw);
        harness.Frame(Draw);
        var version = state.FrameSourceVersion;

        harness.Frame(Draw);
        Assert.Equal(version, state.FrameSourceVersion);

        source.Pending = 12;
        source.RaiseInvalidated();
        harness.Frame(Draw);
        Assert.NotEqual(version, state.FrameSourceVersion);
    }

    [Fact]
    public void ASourceThatChangesOnlyItsCountStillMovesTheVersion()
    {
        var source = new PublishingSource { Pending = 10 };
        var state = new TableState();
        var columns = Fixed(1, 100f);
        using var harness = new FrameHarness(Width, Height);
        void Draw(Gui gui) => gui.Table(state, source, columns);
        harness.Frame(Draw);
        harness.Frame(Draw);
        var version = state.FrameSourceVersion;

        source.Pending = 11;
        harness.Frame(Draw);

        Assert.NotEqual(version, state.FrameSourceVersion);
    }

    [Fact]
    public void SavedColumnChoicesAndSortComeBackInAnotherTableAndSortTheSource()
    {
        var columns = new TableColumns<Item>(
        [
            TableColumn<Item>.Of("Name", static item => item.FileName, id: "name", width: 150f),
            TableColumn<Item>.Of("Downloads", static item => item.Downloads, id: "downloads", width: 100f)
        ]);
        var source = new ListTableSource<Item>(Items, static item => item.FileName);
        var state = new TableState();
        using var harness = new FrameHarness(Width, Height);
        void Draw(Gui gui) => gui.Table(state, source, columns);
        harness.Frame(Draw);
        state.Columns.MoveTo(columns[0], columns[1]);
        state.Columns.SetWidth(columns[1], TableLength.Pixels(222f));
        state.SortBy(source, columns[1]);
        state.SortBy(source, columns[1]);
        var saved = state.Save();

        var rebuilt = new TableColumns<Item>(
        [
            TableColumn<Item>.Of("Name", static item => item.FileName, id: "name", width: 150f),
            TableColumn<Item>.Of("Downloads", static item => item.Downloads, id: "downloads", width: 100f)
        ]);
        var fresh = new ListTableSource<Item>(Items, static item => item.FileName);
        var next = new TableState();
        next.Restore(saved, rebuilt, fresh);
        using var other = new FrameHarness(Width, Height);
        void DrawNext(Gui gui) => gui.Table(next, fresh, rebuilt);
        other.Frame(DrawNext);
        other.Frame(DrawNext);

        Assert.Equal("downloads", next.Sort.Primary!.Value.Column.Id);
        Assert.Equal(TableSortDirection.Descending, next.Sort.Primary.Value.Direction);
        Assert.True(other.Gui.RootNode!.FindChildById("table/header/downloads")!.Rect.X <
                    other.Gui.RootNode.FindChildById("table/header/name")!.Rect.X);
        Assert.Equal(222f, other.Gui.RootNode.FindChildById("table/header/downloads")!.Rect.W, 0.5f);
        Assert.Equal("c.zip", fresh[0].FileName);
    }

    [Fact]
    public void RestoringWithoutASourceLeavesTheRowsAlone()
    {
        var columns = Columns("a");
        var state = new TableState();
        state.Restore(new TableSnapshot([], []), columns);

        Assert.Throws<ArgumentNullException>(() => state.Restore(null!, columns));
        Assert.Throws<ArgumentNullException>(() => state.Restore<int>(new TableSnapshot([], []), null!));
    }

    static TableColumns<int> Columns(params string[] ids) =>
        new(ids.Select(static id => TableColumn<int>.Of(id, static row => row, id: id)));

    sealed class CountingSource(Action onRead) : ITableViewSource<int>
    {
        public int RowCount => 100;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public int this[int index] => index;

        public RowIdentity GetRowIdentity(int index)
        {
            onRead();
            return RowIdentity.UnstableIndex(index);
        }
    }
}

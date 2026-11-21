using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the sort list: how a click promotes a column and demotes the old primary, how a programmatic apply
/// writes the same list, that keys follow column instances and survive a rebuilt column list, how the order is
/// saved and restored, and that row identity does not move with the order.
/// </summary>
public class TableSortListTests
{
    [Fact]
    public void AnEmptyListHasNoPrimary()
    {
        var list = new TableSortList();

        Assert.Null(list.Primary);
        Assert.Empty(list);
        Assert.Equal(0, list.SecondaryCount);
        Assert.Equal(-1, list.IndexOf(Col("name")));
    }

    [Fact]
    public void AClickPromotesTheColumnAndDemotesThePreviousPrimaryKeepingEarlierKeysInOrder()
    {
        var (name, category, size) = (Col("name"), Col("category"), Col("size"));
        var list = new TableSortList();

        list.Click(name);
        Assert.Equal([name], Columns(list));

        list.Click(category);
        Assert.Equal([category, name], Columns(list));

        list.Click(size);
        Assert.Equal([size, category, name], Columns(list));
        Assert.Equal(2, list.SecondaryCount);
        Assert.All(list, static key => Assert.Equal(TableSortDirection.Ascending, key.Direction));
    }

    [Fact]
    public void ClickingARetainedKeyMovesItToTheFrontWithoutRepeatingIt()
    {
        var (name, category, size) = (Col("name"), Col("category"), Col("size"));
        var list = new TableSortList();
        list.Set([new TableSort(size), new TableSort(category), new TableSort(name)]);

        list.Click(name);

        Assert.Equal([name, size, category], Columns(list));
    }

    [Fact]
    public void ClickingThePrimaryAgainFlipsItsDirectionAndKeepsTheSecondaries()
    {
        var (name, size) = (Col("name"), Col("size"));
        var list = new TableSortList();
        list.Set([new TableSort(size), new TableSort(name)]);

        list.Click(size);
        Assert.Equal(TableSortDirection.Descending, list[0].Direction);
        Assert.Equal([size, name], Columns(list));

        list.Click(size);
        Assert.Equal(TableSortDirection.Ascending, list[0].Direction);
    }

    [Fact]
    public void PromoteKeepsTheDirectionItWasGiven()
    {
        var (name, size) = (Col("name"), Col("size"));
        var list = new TableSortList();
        list.Promote(new TableSort(name));

        list.Promote(new TableSort(size, TableSortDirection.Descending));

        Assert.Equal(TableSortDirection.Descending, list.Primary!.Value.Direction);
        Assert.Equal([size, name], Columns(list));
    }

    [Fact]
    public void SetReplacesTheOrderAndAColumnGivenTwiceKeepsItsFirstPosition()
    {
        var (old, a, b) = (Col("old"), Col("a"), Col("b"));
        var list = new TableSortList();
        list.Click(old);

        list.Set([new TableSort(a), new TableSort(b, TableSortDirection.Descending), new TableSort(a)]);

        Assert.Equal([a, b], Columns(list));
        Assert.Equal(TableSortDirection.Ascending, list[0].Direction);
    }

    [Fact]
    public void KeysAreComparedByColumnInstanceAndNotByIdOrTitle()
    {
        var first = Col("same");
        var second = Col("same");
        var list = new TableSortList();

        list.Set([new TableSort(first), new TableSort(second)]);

        Assert.Equal(2, list.Count);
        Assert.Equal(0, list.IndexOf(first));
        Assert.Equal(1, list.IndexOf(second));
    }

    [Fact]
    public void ClearDropsThePrimaryAndEveryRetainedKey()
    {
        var list = new TableSortList();
        list.Set([new TableSort(Col("a")), new TableSort(Col("b")), new TableSort(Col("c"))]);

        list.Clear();

        Assert.Empty(list);
        Assert.Null(list.Primary);
    }

    [Fact]
    public void EveryChangeMovesTheVersion()
    {
        var list = new TableSortList();
        var version = list.Version;

        list.Click(Col("a"));
        Assert.True(list.Version > version);
        version = list.Version;
        list.Promote(new TableSort(Col("b")));
        Assert.True(list.Version > version);
        version = list.Version;
        list.Set([new TableSort(Col("c"))]);
        Assert.True(list.Version > version);
        version = list.Version;
        list.Clear();
        Assert.True(list.Version > version);
    }

    [Fact]
    public void ANullColumnIsRejected()
    {
        var list = new TableSortList();

        Assert.Throws<ArgumentNullException>(() => list.Click(null!));
        Assert.Throws<ArgumentNullException>(() => list.Promote(default));
        Assert.Throws<ArgumentNullException>(() => list.Set([default]));
    }

    [Fact]
    public void SortByUpdatesTheListAndHandsTheSourceTheWholeOrder()
    {
        var columns = Table();
        var state = new TableState();
        var source = new RecordingSource();

        state.SortBy(source, columns.Name);
        state.SortBy(source, columns.Size);

        var received = source.Calls.Last();
        Assert.Equal([columns.Size, columns.Name], received.Select(static key => key.Column));
        Assert.Equal([columns.Size, columns.Name], Columns(state.Sort));
    }

    [Fact]
    public void SortByAnExplicitListAppliesItAndAnEmptyListClears()
    {
        var columns = Table();
        var state = new TableState();
        var source = new RecordingSource();

        state.SortBy(source, [new TableSort(columns.Name), new TableSort(columns.Size, TableSortDirection.Descending)]);
        Assert.Equal([columns.Name, columns.Size], Columns(state.Sort));

        state.SortBy(source, []);
        Assert.Empty(state.Sort);
        Assert.Empty(source.Calls.Last());
    }

    [Fact]
    public void SortByRefusesAColumnThatCannotBeSortedAndChangesNothing()
    {
        var columns = Table();
        var state = new TableState();
        var source = new RecordingSource();
        state.SortBy(source, columns.Name);
        var calls = source.Calls.Count;

        var error = Assert.Throws<ArgumentException>(() => state.SortBy(source, columns.Actions));

        Assert.Contains("cannot be sorted", error.Message);
        Assert.Throws<ArgumentException>(() => state.SortBy(source, [new TableSort(columns.Actions)]));
        Assert.Equal([columns.Name], Columns(state.Sort));
        Assert.Equal(calls, source.Calls.Count);
    }

    [Fact]
    public void SortByRefusesAColumnThatIsNotOneOfTheTablesColumns()
    {
        var columns = Table();
        var stranger = Table();
        var state = new TableState();
        var source = new RecordingSource();
        DrawOnce(state, new RecordingSource(), columns.All);

        var error = Assert.Throws<ArgumentException>(() => state.SortBy(source, stranger.Name));

        Assert.Contains("not one of this table's columns", error.Message);
        Assert.Empty(state.Sort);
        Assert.Empty(source.Calls);
    }

    [Fact]
    public void BeforeTheFirstFrameThereIsNoColumnListToCheckAgainst()
    {
        var columns = Table();
        var state = new TableState();

        state.SortBy(new RecordingSource(), columns.Name);

        Assert.Equal([columns.Name], Columns(state.Sort));
    }

    [Fact]
    public void SavingGivesIdsAndDirectionsInEvaluationOrder()
    {
        var columns = Table();
        var list = new TableSortList();
        list.Set([new TableSort(columns.Size, TableSortDirection.Descending), new TableSort(columns.Name)]);

        var saved = list.Save();

        Assert.Equal([("size", TableSortDirection.Descending), ("name", TableSortDirection.Ascending)], saved);
    }

    [Fact]
    public void RestoringResolvesIdsAgainstTheCurrentColumnsAndSilentlyDropsWhatIsGone()
    {
        var columns = Table();
        var list = new TableSortList();

        list.Restore(
        [
            ("size", TableSortDirection.Descending),
            ("removed-long-ago", TableSortDirection.Ascending),
            ("actions", TableSortDirection.Ascending),
            ("name", TableSortDirection.Ascending),
            ("size", TableSortDirection.Ascending)
        ], columns.All);

        Assert.Equal([columns.Size, columns.Name], Columns(list));
        Assert.Equal(TableSortDirection.Descending, list[0].Direction);
    }

    [Fact]
    public void RestoringWhatWasSavedFromAnOlderColumnListRoundTrips()
    {
        var before = Table();
        var original = new TableSortList();
        original.Set([new TableSort(before.Category, TableSortDirection.Descending), new TableSort(before.Name)]);
        var saved = original.Save();

        var after = Table();
        var restored = new TableSortList();
        restored.Restore(saved, after.All);

        Assert.Equal([after.Category, after.Name], Columns(restored));
        Assert.DoesNotContain(before.Category, Columns(restored));
        Assert.Equal(TableSortDirection.Descending, restored[0].Direction);
    }

    [Fact]
    public void RestoringTheSameOrderDoesNotMoveTheVersion()
    {
        var columns = Table();
        var list = new TableSortList();
        list.Set([new TableSort(columns.Name)]);
        var version = list.Version;

        list.Restore(list.Save(), columns.All);

        Assert.Equal(version, list.Version);
    }

    [Fact]
    public void KeysFollowTheirColumnsToARebuiltColumnListById()
    {
        var first = Table();
        var state = new TableState();
        var source = new RecordingSource();
        state.SortBy(source, [new TableSort(first.Size, TableSortDirection.Descending), new TableSort(first.Name)]);
        DrawOnce(state, source, first.All);
        var version = state.Sort.Version;

        var rebuilt = Table();
        DrawOnce(state, source, rebuilt.All);

        Assert.Equal([rebuilt.Size, rebuilt.Name], Columns(state.Sort));
        Assert.Equal(TableSortDirection.Descending, state.Sort[0].Direction);
        Assert.True(state.Sort.Version > version);
    }

    [Fact]
    public void ARebuiltColumnListDropsKeysWhoseColumnsAreGone()
    {
        var first = Table();
        var state = new TableState();
        var source = new RecordingSource();
        state.SortBy(source, [new TableSort(first.Size), new TableSort(first.Category)]);
        DrawOnce(state, source, first.All);

        var smaller = new TableColumns<Item>(
        [
            TableColumn<Item>.Of("Category", static item => item.Category),
            TableColumn<Item>.Of("Name", static item => item.Name)
        ]);
        DrawOnce(state, source, smaller);

        Assert.Equal([smaller[0]], Columns(state.Sort));
    }

    [Fact]
    public void DrawingTheSameColumnsAgainLeavesTheSortAlone()
    {
        var columns = Table();
        var state = new TableState();
        var source = new RecordingSource();
        state.SortBy(source, columns.Name);
        DrawOnce(state, source, columns.All);
        var version = state.Sort.Version;

        DrawOnce(state, source, columns.All);
        DrawOnce(state, source, columns.All);

        Assert.Equal(version, state.Sort.Version);
        Assert.Equal([columns.Name], Columns(state.Sort));
    }

    [Fact]
    public void RowIdentityFollowsTheDataAndNotTheIndexWhenTheOrderChanges()
    {
        var recorder = new IdentityRecorder();
        var size = TableColumn<Item>.Of("Size", static item => item.Size);
        var name = TableColumn<Item>.Of("Name", static item => item.Name, renderer: recorder);
        var columns = new TableColumns<Item>([name, size]);
        var items = Enumerable.Range(0, 8).Select(static i => new Item($"item{i}", "c", i)).ToList();
        var source = new ListTableSource<Item>(items, static item => item.Name);
        var state = new TableState();
        using var harness = new FrameHarness(400, 300);
        void Draw(Gui gui) => gui.Table(state, source, columns);

        harness.Frame(Draw);
        harness.Frame(Draw);
        var before = recorder.Latest();

        state.SortBy(source, [new TableSort(size, TableSortDirection.Descending)]);
        harness.Frame(Draw);
        harness.Frame(Draw);
        var after = recorder.Latest();

        Assert.Equal(8, before.Count);
        Assert.Equal(before.Select(static cell => cell.Key).Order(), after.Select(static cell => cell.Key).Order());
        Assert.All(after, cell => Assert.Equal(cell.Key, cell.Name));
        Assert.NotEqual(before.Select(static cell => cell.Key), after.Select(static cell => cell.Key));
        Assert.Equal(before.Select(static cell => cell.Key).Reverse(), after.Select(static cell => cell.Key));
    }

    static TableColumn Col(string title) => TableColumn<int>.Of(title, static row => row, id: title);

    static TableColumn[] Columns(IEnumerable<TableSort> keys) => [.. keys.Select(static key => key.Column)];

    /// <summary>The columns of a small table over <see cref="Item"/>, built fresh so each call has new instances.</summary>
    static ItemColumns Table() => new();

    static void DrawOnce(TableState state, RecordingSource source, TableColumns<Item> columns)
    {
        using var harness = new FrameHarness(400, 300);
        harness.Frame(gui => gui.Table(state, source, columns));
    }

    sealed record Item(string Name, string Category, int Size);

    sealed class ItemColumns
    {
        public TableColumn<Item, string> Name { get; } = TableColumn<Item>.Of("Name", static item => item.Name);

        public TableColumn<Item, string> Category { get; } = TableColumn<Item>.Of("Category", static item => item.Category);

        public TableColumn<Item, int> Size { get; } = TableColumn<Item>.Of("Size", static item => item.Size);

        public TableColumn<Item, Item> Actions { get; } = TableColumn<Item>.Row("Actions");

        public TableColumns<Item> All => field ??= new TableColumns<Item>([Name, Category, Size, Actions]);
    }

    /// <summary>A sortable source that only remembers what it was asked to do.</summary>
    sealed class RecordingSource : ISortableTableViewSource<Item>
    {
        public List<IReadOnlyList<TableSort>> Calls { get; } = [];

        public int RowCount => 3;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public Item this[int index] => new($"item{index}", "c", index);

        public RowIdentity GetRowIdentity(int index) => new($"item{index}");

        public void ApplySort(IReadOnlyList<TableSort> keys) => Calls.Add(keys);
    }

    /// <summary>Reads each cell's identity while rendering, as a renderer must: the accessor is lazy.</summary>
    sealed class IdentityRecorder : ITableCellRenderer<Item, string>
    {
        readonly List<List<(string Key, string Name)>> _frames = [];

        public void Render(Gui gui, in TableCellContext<Item, string> cell)
        {
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            if (cell.Index == 0)
            {
                _frames.Add([]);
            }

            _frames[^1].Add((cell.Identity.Key, cell.Value));
        }

        public List<(string Key, string Name)> Latest() => _frames[^1];
    }
}

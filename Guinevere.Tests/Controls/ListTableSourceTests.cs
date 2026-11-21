namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the built-in in-memory source: the order it produces for one and several keys, that it never touches
/// the rows, that identity follows the row and not its position, that every change shows only at the next frame
/// start, that a replaced list keeps sort and identities, and that it sorts without allocating beyond its one
/// permutation.
/// </summary>
public class ListTableSourceTests
{
    static readonly TableColumn<Item> Name = TableColumn<Item>.Of("Name", static item => item.Name);
    static readonly TableColumn<Item> Category = TableColumn<Item>.Of("Category", static item => item.Category);
    static readonly TableColumn<Item> Size = TableColumn<Item>.Of("Size", static item => item.Size);
    static readonly TableColumn<Item> Opaque = TableColumn<Item>.Of("Opaque", static item => item.Opaque);
    static readonly TableColumn<Item> Whole = TableColumn<Item>.Row("Whole");

    static long _frames;

    [Fact]
    public void WithNoSortTheRowsComeInTheirOwnOrderWithTheirIdentity()
    {
        var source = Source([new(1, "b", "x", 3), new(2, "a", "x", 1)]);

        Assert.Equal(2, source.RowCount);
        Assert.Equal("b", source[0].Name);
        Assert.Equal("1", source.GetRowIdentity(0).Key);
        Assert.Equal("2", source.GetRowIdentity(1).Key);
    }

    [Fact]
    public void AKeySortsAscendingOrDescending()
    {
        var source = Source([new(1, "b", "x", 3), new(2, "a", "x", 1), new(3, "c", "x", 2)]);

        Sort(source, new TableSort(Name));
        Assert.Equal(["a", "b", "c"], Names(source));

        Sort(source, new TableSort(Name, TableSortDirection.Descending));
        Assert.Equal(["c", "b", "a"], Names(source));
    }

    [Fact]
    public void AGivenComparerDecidesTheOrderInsteadOfTheTypesOwn()
    {
        var reversed = TableColumn<Item>.Of("Reversed", static item => item.Size,
            comparer: Comparer<int>.Create(static (a, b) => b.CompareTo(a)));
        var source = Source([new(1, "a", "x", 1), new(2, "b", "x", 3), new(3, "c", "x", 2)]);

        Sort(source, new TableSort(reversed));

        Assert.Equal(["b", "c", "a"], Names(source));
    }

    [Fact]
    public void ARowValuedColumnWithAComparerSorts()
    {
        var byName = TableColumn<Item>.Row("Whole", comparer: Comparer<Item>.Create(
            static (a, b) => string.CompareOrdinal(a.Name, b.Name)));
        var source = Source([new(1, "b", "x", 0), new(2, "a", "x", 0)]);

        Sort(source, new TableSort(byName));

        Assert.Equal(["a", "b"], Names(source));
    }

    [Fact]
    public void SeveralKeysBreakTiesInOrderAndEqualRowsKeepTheirOriginalOrder()
    {
        var source = Source(
        [
            new(1, "p", "b", 2), new(2, "q", "a", 2), new(3, "r", "b", 1),
            new(4, "s", "a", 2), new(5, "t", "b", 2), new(6, "u", "a", 1)
        ]);

        Sort(source, new TableSort(Category), new TableSort(Size, TableSortDirection.Descending), new TableSort(Name));
        Assert.Equal(["q", "s", "u", "p", "t", "r"], Names(source));

        Sort(source, new TableSort(Category), new TableSort(Size));
        Assert.Equal(["u", "q", "s", "r", "p", "t"], Names(source));
    }

    [Fact]
    public void AnEmptyKeyListRestoresTheOriginalOrder()
    {
        var source = Source([new(1, "b", "x", 0), new(2, "a", "x", 0)]);
        Sort(source, new TableSort(Name));
        Assert.Equal(["a", "b"], Names(source));

        Sort(source);

        Assert.Equal(["b", "a"], Names(source));
    }

    [Fact]
    public void EveryChangeRaisesInvalidated()
    {
        var source = Source([new(1, "a", "x", 0)]);
        var raised = 0;
        source.Invalidated += (_, _) => raised++;

        source.ApplySort([new TableSort(Name)]);
        source.Refresh();
        source.Replace([new Item(2, "b", "x", 0)]);

        Assert.Equal(3, raised);
    }

    [Fact]
    public void AColumnThatIsNotSortableOrNotOverThisRowTypeIsRejectedAndLeavesTheOrderAlone()
    {
        var source = Source([new(1, "b", "x", 0), new(2, "a", "x", 0)]);
        var foreign = TableColumn<int>.Of("Foreign", static row => row);

        Assert.Throws<ArgumentException>(() => source.ApplySort([new TableSort(Opaque)]));
        Assert.Throws<ArgumentException>(() => source.ApplySort([new TableSort(Whole)]));
        Assert.Throws<ArgumentException>(() => source.ApplySort([new TableSort(Name), new TableSort(foreign)]));
        Publish(source);

        Assert.Equal(["b", "a"], Names(source));
    }

    [Fact]
    public void ARowKeepsItsIdentityWhenTheOrderChanges()
    {
        var source = Source([new(10, "b", "x", 0), new(20, "a", "x", 0), new(30, "c", "x", 0)]);
        var before = source.GetRowIdentity(0);
        Assert.Equal("b", source[0].Name);

        Sort(source, new TableSort(Name));

        Assert.Equal("a", source[0].Name);
        Assert.Equal("20", source.GetRowIdentity(0).Key);
        Assert.Equal("10", source.GetRowIdentity(1).Key);
        Assert.Equal(before.Key, source.GetRowIdentity(1).Key);
    }

    [Fact]
    public void AChangeOnlyShowsAtTheNextFrameStartNeverInTheMiddleOfAFrame()
    {
        var source = Source([new(1, "b", "x", 0), new(2, "a", "x", 0)]);
        Publish(source);

        source.ApplySort([new TableSort(Name)]);
        source.Replace([new Item(9, "z", "x", 0)]);

        Assert.Equal(2, source.RowCount);
        Assert.Equal(["b", "a"], Names(source));

        Publish(source);

        Assert.Equal(1, source.RowCount);
        Assert.Equal(["z"], Names(source));
    }

    [Fact]
    public void PublishingTwiceForTheSameFrameDoesNothingTheSecondTime()
    {
        var source = Source([new(1, "b", "x", 0)]);
        source.BeginFrame(7);

        source.Replace([new Item(2, "a", "x", 0), new Item(3, "c", "x", 0)]);
        source.BeginFrame(7);
        Assert.Equal(1, source.RowCount);

        source.BeginFrame(8);
        Assert.Equal(2, source.RowCount);
    }

    [Fact]
    public void AReplacedListKeepsTheSortAndTheIdentitiesOfRowsThatSurvive()
    {
        var source = Source([new(1, "c", "x", 0), new(2, "a", "x", 0), new(3, "b", "x", 0)]);
        Sort(source, new TableSort(Name));
        Assert.Equal("2", source.GetRowIdentity(0).Key);

        source.Replace([new Item(1, "c", "x", 0), new Item(2, "a2", "x", 0), new Item(3, "b", "x", 0), new Item(4, "d", "x", 0)]);
        Publish(source);

        Assert.Equal(["a2", "b", "c", "d"], Names(source));
        Assert.Equal(["2", "3", "1", "4"], Identities(source));
    }

    [Fact]
    public async Task ReplaceMayBeCalledFromAnotherThread()
    {
        var source = Source([new(1, "a", "x", 0)]);
        var raisedOn = -1;
        source.Invalidated += (_, _) => raisedOn = Environment.CurrentManagedThreadId;

        var workerThread = -1;
        await Task.Run(() =>
        {
            workerThread = Environment.CurrentManagedThreadId;
            source.Replace([new Item(2, "b", "x", 0), new Item(3, "c", "x", 0)]);
        }, TestContext.Current.CancellationToken);
        Publish(source);

        Assert.Equal(2, source.RowCount);
        Assert.Equal(["b", "c"], Names(source));
        Assert.Equal(workerThread, raisedOn);
    }

    [Fact]
    public void AListEditedInPlaceBetweenFramesIsNoticedThroughItsCountAndKeepsTheSort()
    {
        List<Item> rows = [new(1, "c", "x", 0), new(2, "a", "x", 0)];
        var source = new ListTableSource<Item>(rows, static item => item.Id.ToString());
        Sort(source, new TableSort(Name));

        rows.Add(new(3, "b", "x", 0));
        Assert.Equal(2, source.RowCount);
        Publish(source);

        Assert.Equal(3, source.RowCount);
        Assert.Equal(["a", "b", "c"], Names(source));
    }

    [Fact]
    public void ARefreshAfterAnEditInPlaceResortsTheRows()
    {
        var rows = new Item[] { new(1, "a", "x", 0), new(2, "b", "x", 0) };
        var source = new ListTableSource<Item>(rows, static item => item.Id.ToString());
        Sort(source, new TableSort(Name));

        rows[0] = rows[0] with { Name = "z" };
        source.Refresh();
        Publish(source);

        Assert.Equal(["b", "z"], Names(source));
    }

    [Fact]
    public void LoadingIsAFlagTheOwnerSets()
    {
        var source = Source([]);

        Assert.False(source.IsLoading);
        source.IsLoading = true;
        Assert.True(source.IsLoading);
    }

    [Fact]
    public void ItNeedsRowsAnIdentityAndANewListToReplaceWith()
    {
        Assert.Throws<ArgumentNullException>(static () => new ListTableSource<Item>(null!, static item => item.Name));
        Assert.Throws<ArgumentNullException>(static () => new ListTableSource<Item>([], null!));
        Assert.Throws<ArgumentNullException>(static () => Source([]).Replace(null!));
    }

    [Fact]
    public void ItAgreesWithLinqOverManyRandomRows()
    {
        var random = new Random(66);
        var rows = Enumerable.Range(0, 5000)
            .Select(i => new Item(i, "n" + random.Next(50), "c" + random.Next(5), random.Next(20)))
            .ToArray();
        var source = new ListTableSource<Item>(rows, static item => item.Id.ToString());

        Sort(source, new TableSort(Category), new TableSort(Size, TableSortDirection.Descending), new TableSort(Name));

        var expected = rows.OrderBy(static r => r.Category, Comparer<string>.Default)
            .ThenByDescending(static r => r.Size).ThenBy(static r => r.Name, Comparer<string>.Default)
            .Select(static r => r.Id);
        Assert.Equal(expected, Enumerable.Range(0, source.RowCount).Select(i => source[i].Id));
    }

    [Fact]
    public void AnotherSortOfTheSameCountAllocatesNothingAndNeitherDoesAReadOfARow()
    {
        var rows = Enumerable.Range(0, 20_000).Select(static i => new Item(i, "n" + (i * 7919 % 1000), "c" + i % 9, i % 100)).ToArray();
        var source = new ListTableSource<Item>(rows, static item => item.Id.ToString());
        TableSort[] keys = [new(Category), new(Size, TableSortDirection.Descending), new(Name)];
        source.ApplySort(keys);
        Publish(source);
        var frame = Interlocked.Increment(ref _frames);

        var before = GC.GetAllocatedBytesForCurrentThread();
        source.ApplySort(keys);
        source.BeginFrame(frame);
        var sorting = GC.GetAllocatedBytesForCurrentThread() - before;

        before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            _ = source[i];
        }

        var reading = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, sorting);
        Assert.Equal(0, reading);
    }

    static ListTableSource<Item> Source(Item[] rows) => new(rows, static item => item.Id.ToString());

    static void Publish<TRow>(ListTableSource<TRow> source) => source.BeginFrame(Interlocked.Increment(ref _frames));

    /// <summary>Applies the keys and publishes them, as the next frame start would.</summary>
    static void Sort(ListTableSource<Item> source, params TableSort[] keys)
    {
        source.ApplySort(keys);
        Publish(source);
    }

    static string[] Names(ListTableSource<Item> source) =>
        [.. Enumerable.Range(0, source.RowCount).Select(i => source[i].Name)];

    static string[] Identities(ListTableSource<Item> source) =>
        [.. Enumerable.Range(0, source.RowCount).Select(i => source.GetRowIdentity(i).Key)];

    /// <summary>A row; <see cref="Opaque"/> has no natural order.</summary>
    sealed record Item(int Id, string Name, string Category, int Size)
    {
        public Unordered Opaque => new(Size);
    }

    readonly record struct Unordered(int Weight);
}

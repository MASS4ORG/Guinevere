using System.Globalization;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers what the table reuses between frames, and the cases that must make it stop: new columns, a changed
/// value, a changed culture, and a row slot taken over by another row.
/// </summary>
public class TableCacheTests
{
    [Fact]
    public void TheSameColumnsAreNotRebuiltAndANewSetIs()
    {
        var cache = new TableCache();
        var first = Columns("a", "b");
        var second = Columns("a", "b");

        Assert.True(cache.Begin(first.Set, new TableColumnState()));
        Assert.False(cache.Begin(first.Set, new TableColumnState()));
        Assert.Same(first.Set, cache.Columns);

        Assert.True(cache.Begin(second.Set, new TableColumnState()));
        Assert.Same(second.Set, cache.Columns);
    }

    [Fact]
    public void TheSetKnowsItsShownColumnsHeaderIdsAndCellSuffixes()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row, id: "a"),
            TableColumn<int>.Of("B", static row => row, id: "b", visible: false),
            TableColumn<int>.Of("C", static row => row, id: "c")
        ]);

        var view = columns.Set.Arrange();
        Assert.Equal(["a", "c"], view.Shown.Select(static column => column.Id));
        Assert.Equal(["table/header/a", "table/header/c"], view.HeaderIds);
        Assert.Equal(["/a", "/c"], view.CellSuffixes);
        Assert.Equal(view.Shown, view.Typed<int>());
    }

    [Fact]
    public void ChangedChoicesRearrangeTheSameColumnsAndKeepWhatDoesNotDependOnThem()
    {
        var cache = new TableCache();
        var columns = Columns("a", "b");
        var choices = new TableColumnState();
        cache.Begin(columns.Set, choices);
        cache.StoreAutoWidth(columns[0], 12f, 8f, 44f);
        cache.StoreLayout(TableLayout.Resolve(cache.View!.Shown, cache.View.Lengths, 400f, [0f, 0f]), 400f, 12f, 8f);
        var view = cache.View;
        var generation = cache.Generation;

        Assert.False(cache.Begin(columns.Set, choices));
        Assert.Same(view, cache.View);

        choices.MoveTo(columns[0], columns[1]);

        Assert.False(cache.Begin(columns.Set, choices));
        Assert.NotSame(view, cache.View);
        Assert.Equal(["b", "a"], cache.View!.Shown.Select(static column => column.Id));
        Assert.True(cache.Generation > generation);
        Assert.False(cache.TryLayout(400f, 12f, 8f, out _));
        Assert.True(cache.TryAutoWidth(columns[0], 12f, 8f, out var auto));
        Assert.Equal(44f, auto);
    }

    [Fact]
    public void TheSetFindsAColumnByIdAndOnlyContainsTheExactInstance()
    {
        var columns = Columns("a", "b");
        var impostor = TableColumn<int>.Of("A", static row => row, id: "a");

        Assert.Same(columns[1], columns.Set.Find("b"));
        Assert.Null(columns.Set.Find("zzz"));
        Assert.True(columns.Set.Contains(columns[0]));
        Assert.False(columns.Set.Contains(impostor));
    }

    [Fact]
    public void ALayoutIsReusedOnlyForTheInputsItWasResolvedFrom()
    {
        var cache = new TableCache();
        var columns = Columns("a");
        cache.Begin(columns.Set, new TableColumnState());
        var view = columns.Set.Arrange();
        var layout = TableLayout.Resolve(view.Shown, view.Lengths, 400f, [0f]);

        Assert.False(cache.TryLayout(400f, 12f, 8f, out _));
        cache.StoreLayout(layout, 400f, 12f, 8f);

        Assert.True(cache.TryLayout(400f, 12f, 8f, out var reused));
        Assert.Same(layout, reused);
        Assert.False(cache.TryLayout(401f, 12f, 8f, out _));
        Assert.False(cache.TryLayout(400f, 13f, 8f, out _));
        Assert.False(cache.TryLayout(400f, 12f, 9f, out _));

        cache.Begin(Columns("a", "b").Set, new TableColumnState());
        Assert.False(cache.TryLayout(400f, 12f, 8f, out _));
    }

    [Fact]
    public void AnAutoWidthIsRememberedPerColumnInstanceTextSizeAndPadding()
    {
        var cache = new TableCache();
        var columns = Columns("a");
        var other = TableColumn<int>.Of("A", static row => row, id: "a");

        Assert.False(cache.TryAutoWidth(columns[0], 12f, 8f, out _));
        cache.StoreAutoWidth(columns[0], 12f, 8f, 51f);

        Assert.True(cache.TryAutoWidth(columns[0], 12f, 8f, out var width));
        Assert.Equal(51f, width);
        Assert.False(cache.TryAutoWidth(columns[0], 14f, 8f, out _));
        Assert.False(cache.TryAutoWidth(columns[0], 12f, 10f, out _));
        Assert.False(cache.TryAutoWidth(other, 12f, 8f, out _));
    }

    [Fact]
    public void ARowKeepsItsIdStringsWhileItStaysInView()
    {
        var cache = Ready(5, "a", "b");
        var slot = cache.Slot(3);

        var id = TableCache.RowId(slot);
        Assert.Equal("table/row3", id);
        Assert.Same(id, TableCache.RowId(cache.Slot(3)));
        Assert.Equal("table/row3/padLeft", TableCache.PadLeftId(slot));
        Assert.Equal("table/row3/padRight", TableCache.PadRightId(slot));
        Assert.Equal("table/row3/b", cache.CellId(slot, 1));
        Assert.Same(cache.CellId(slot, 1), cache.CellId(cache.Slot(3), 1));
    }

    [Fact]
    public void ASlotTakenOverByAnotherRowStartsEmpty()
    {
        var cache = Ready(5, "a");
        var slot = cache.Slot(3);
        Assert.Equal("table/row3/a", cache.CellId(slot, 0));

        // 16 slots: row 19 lands where row 3 was.
        var taken = cache.Slot(19);
        Assert.Same(slot, taken);
        Assert.Equal("table/row19", TableCache.RowId(taken));
        Assert.Equal("table/row19/a", cache.CellId(taken, 0));
    }

    [Fact]
    public void ChangingTheColumnsEmptiesEverySlot()
    {
        var cache = Ready(5, "a");
        Assert.Equal("table/row2/a", cache.CellId(cache.Slot(2), 0));

        cache.Begin(Columns("z").Set, new TableColumnState());

        Assert.Equal("table/row2/z", cache.CellId(cache.Slot(2), 0));
    }

    [Fact]
    public void AStringIsItsOwnTextAndNullIsEmpty()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);
        var text = new string('x', 3);

        Assert.Same(text, cache.TextCacheFor<string>(0).Text(cache, slot, text));
        Assert.Equal("", cache.TextCacheFor<string>(0).Text(cache, slot, null!));
        Assert.Equal("", cache.TextCacheFor<int?>(0).Text(cache, slot, null));
    }

    [Fact]
    public void AValueTypeIsFormattedOnceWhileItStaysEqualAndAgainWhenItChanges()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);
        var text = cache.TextCacheFor<int>(1 - 1);

        var first = text.Text(cache, slot, 1234);
        Assert.Equal("1234", first);
        Assert.Same(first, text.Text(cache, slot, 1234));

        Assert.Equal("1235", text.Text(cache, slot, 1235));
    }

    [Fact]
    public void TheTextOfAValueIsKeptPerRowAndNotSharedBetweenRows()
    {
        var cache = Ready(5, "a");
        var text = cache.TextCacheFor<int>(0);

        // Large numbers: the runtime keeps one shared string for small ones, which would make every check pass.
        var zero = text.Text(cache, cache.Slot(0), 70_000);
        var one = text.Text(cache, cache.Slot(1), 80_000);

        Assert.Equal("70000", zero);
        Assert.Equal("80000", one);
        Assert.Same(zero, text.Text(cache, cache.Slot(0), 70_000));
        Assert.Same(one, text.Text(cache, cache.Slot(1), 80_000));

        // Row 16 takes the slot of row 0: an equal value is formatted again, never read from row 0's text.
        Assert.NotSame(zero, text.Text(cache, cache.Slot(16), 70_000));
    }

    [Fact]
    public void ANullableValueTypeIsCachedAndAMissingValueIsEmpty()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);
        var text = cache.TextCacheFor<int?>(0);

        var present = text.Text(cache, slot, 12);

        Assert.Same(present, text.Text(cache, slot, 12));
        Assert.Equal("", text.Text(cache, slot, null));
        Assert.Equal("12", text.Text(cache, slot, 12));
    }

    [Fact]
    public void AMutableObjectIsFormattedEveryTimeSoAChangeBehindItsReferenceShows()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);
        var text = cache.TextCacheFor<Mutable>(0);
        var item = new Mutable { Text = "before" };

        Assert.Equal("before", text.Text(cache, slot, item));
        item.Text = "after";

        Assert.Equal("after", text.Text(cache, slot, item));
    }

    [Fact]
    public void AChangedCultureFormatsAgain()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var columns = Columns("a");
            var cache = new TableCache();
            cache.Begin(columns.Set, new TableColumnState());
            cache.Reserve(1);
            Assert.Equal("1234.5", cache.TextCacheFor<double>(0).Text(cache, cache.Slot(0), 1234.5));

            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = comma;
            cache.Begin(columns.Set, new TableColumnState());

            Assert.Equal("1234,5", cache.TextCacheFor<double>(0).Text(cache, cache.Slot(0), 1234.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void GrowingTheCacheKeepsTheTextOfTheColumnCorrect()
    {
        var cache = Ready(5, "a");
        var text = cache.TextCacheFor<int>(0);
        Assert.Equal("5", text.Text(cache, cache.Slot(2), 5));

        cache.Reserve(100);

        Assert.True(cache.Capacity >= 200);
        Assert.Equal("5", text.Text(cache, cache.Slot(2), 5));
        Assert.Equal("6", text.Text(cache, cache.Slot(2), 6));
    }

    [Fact]
    public void EachColumnGetsItsOwnTextCacheUntilTheColumnsChange()
    {
        var cache = Ready(1, "a", "b");

        var first = cache.TextCacheFor<int>(0);
        Assert.Same(first, cache.TextCacheFor<int>(0));
        Assert.NotSame(first, cache.TextCacheFor<int>(1));

        cache.Begin(Columns("a", "b").Set, new TableColumnState());
        Assert.NotSame(first, cache.TextCacheFor<int>(0));
    }

    [Fact]
    public void AnUnchangedValueTypeCellCostsNoAllocationAtAll()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);
        var text = cache.TextCacheFor<int>(0);
        _ = text.Text(cache, slot, 123_456);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            _ = text.Text(cache, slot, 123_456);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 256, $"10000 unchanged cells allocated {allocated} bytes: the value was boxed or reformatted");
    }

    static TableColumns<int> Columns(params string[] ids) =>
        new(ids.Select(static id => TableColumn<int>.Of(id.ToUpperInvariant(), static row => row, id: id)));

    /// <summary>A cache over columns with the given ids and room for <paramref name="rows"/> built rows.</summary>
    static TableCache Ready(int rows, params string[] ids)
    {
        var cache = new TableCache();
        cache.Begin(Columns(ids).Set, new TableColumnState());
        cache.Reserve(rows);
        return cache;
    }

    sealed class Mutable
    {
        public string Text { get; set; } = "";

        public override string ToString() => Text;
    }
}

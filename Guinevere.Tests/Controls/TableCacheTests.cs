using System.Globalization;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers what the table reuses between frames, and the cases that must make it stop: a changed value,
/// an edited column list, a changed culture, and a row slot taken over by another row.
/// </summary>
public class TableCacheTests
{
    [Fact]
    public void TheSameColumnsKeepTheSameShownArrayAndEditedColumnsRebuildIt()
    {
        var cache = new TableCache();
        var list = new List<TableColumn> { Column("a"), Column("b") };

        cache.Begin(list);
        var first = cache.Shown;
        cache.Begin(list);
        Assert.Same(first, cache.Shown);

        list.Add(Column("c"));
        cache.Begin(list);
        Assert.NotSame(first, cache.Shown);
        Assert.Equal(3, cache.Shown.Length);

        var swapped = list[0];
        list[0] = list[1];
        list[1] = swapped;
        cache.Begin(list);
        Assert.Equal(["b", "a", "c"], cache.Shown.Select(static column => column.Id.Value));
    }

    [Fact]
    public void HiddenColumnsAreNotShownAndHeadersGetTheirOwnIds()
    {
        var cache = new TableCache();
        cache.Begin([Column("a"), new TableColumn("B", new("b")) { InitiallyVisible = false }, Column("c")]);

        Assert.Equal(["a", "c"], cache.Shown.Select(static column => column.Id.Value));
        Assert.Equal(["table/header/a", "table/header/c"], cache.HeaderIds);
    }

    [Fact]
    public void ADuplicateColumnIdIsRejectedWhenTheListChanges()
    {
        var cache = new TableCache();

        var error = Assert.Throws<ArgumentException>(() => cache.Begin([Column("a"), Column("a")])); Assert.Contains("'a'", error.Message);
    }

    [Fact]
    public void ALayoutIsReusedOnlyForTheInputsItWasResolvedFrom()
    {
        var cache = new TableCache();
        cache.Begin([Column("a")]);
        var layout = TableLayout.Resolve(cache.Shown, 400f, [0f]);

        Assert.False(cache.TryLayout(400f, 12f, 8f, out _));
        cache.StoreLayout(layout, 400f, 12f, 8f);

        Assert.True(cache.TryLayout(400f, 12f, 8f, out var reused));
        Assert.Same(layout, reused);
        Assert.False(cache.TryLayout(401f, 12f, 8f, out _));
        Assert.False(cache.TryLayout(400f, 13f, 8f, out _));
        Assert.False(cache.TryLayout(400f, 12f, 9f, out _));

        cache.Begin([Column("a"), Column("b")]);
        Assert.False(cache.TryLayout(400f, 12f, 8f, out _));
    }

    [Fact]
    public void AnAutoWidthIsRememberedPerColumnTextSizeAndPadding()
    {
        var cache = new TableCache();
        var id = new TableColumnId("id");

        Assert.False(cache.TryAutoWidth(id, 12f, 8f, out _));
        cache.StoreAutoWidth(id, 12f, 8f, 51f);

        Assert.True(cache.TryAutoWidth(id, 12f, 8f, out var width));
        Assert.Equal(51f, width);
        Assert.False(cache.TryAutoWidth(id, 14f, 8f, out _));
        Assert.False(cache.TryAutoWidth(id, 12f, 10f, out _));
        Assert.False(cache.TryAutoWidth(new TableColumnId("other"), 12f, 8f, out _));
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
        Assert.Equal("1.5", cache.Text(slot, 0, 1.5));

        // 16 slots: row 19 lands where row 3 was.
        var taken = cache.Slot(19);
        Assert.Same(slot, taken);
        Assert.Equal("table/row19", TableCache.RowId(taken));
        Assert.Equal("table/row19/a", cache.CellId(taken, 0));
        Assert.Equal("2.5", cache.Text(taken, 0, 2.5));
    }

    [Fact]
    public void ChangingTheColumnsEmptiesEverySlot()
    {
        var cache = Ready(5, "a");
        Assert.Equal("table/row2/a", cache.CellId(cache.Slot(2), 0));

        cache.Begin([Column("z")]);

        Assert.Equal("table/row2/z", cache.CellId(cache.Slot(2), 0));
    }

    [Fact]
    public void AStringIsItsOwnTextAndNullIsEmpty()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);
        var text = new string('x', 3);

        Assert.Same(text, cache.Text(slot, 0, text));
        Assert.Equal("", cache.Text(slot, 0, null));
    }

    [Fact]
    public void AValueTypeIsFormattedOnceWhileItStaysEqualAndAgainWhenItChanges()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);

        var first = cache.Text(slot, 0, 1234);
        Assert.Equal("1234", first);
        Assert.Same(first, cache.Text(slot, 0, 1234));

        Assert.Equal("1235", cache.Text(slot, 0, 1235));
    }

    [Fact]
    public void ACellOfAnotherTypeNeverReusesTheTextOfTheTypeBefore()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);

        Assert.Equal("1", cache.Text(slot, 0, 1));
        Assert.Equal("1", cache.Text(slot, 0, 1L));
        Assert.Equal("True", cache.Text(slot, 0, true));
    }

    [Fact]
    public void AMutableObjectIsFormattedEveryTimeSoAChangeBehindItsReferenceShows()
    {
        var cache = Ready(1, "a");
        var slot = cache.Slot(0);
        var item = new Mutable { Text = "before" };

        Assert.Equal("before", cache.Text(slot, 0, item));
        item.Text = "after";

        Assert.Equal("after", cache.Text(slot, 0, item));
    }

    [Fact]
    public void AChangedCultureFormatsAgain()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var cache = Ready(1, "a");
            Assert.Equal("1234.5", cache.Text(cache.Slot(0), 0, 1234.5));

            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = comma;
            cache.Begin([Column("a")]);

            Assert.Equal("1234,5", cache.Text(cache.Slot(0), 0, 1234.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    static TableColumn Column(string id) => new(id.ToUpperInvariant(), new TableColumnId(id));

    /// <summary>A cache over the given columns with room for <paramref name="rows"/> built rows.</summary>
    static TableCache Ready(int rows, params string[] ids)
    {
        var cache = new TableCache();
        cache.Begin([.. ids.Select(Column)]);
        cache.Reserve(rows);
        return cache;
    }

    sealed class Mutable
    {
        public string Text { get; set; } = "";

        public override string ToString() => Text;
    }
}

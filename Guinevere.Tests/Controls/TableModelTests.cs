namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the table's data model: how columns are built and identified, which of them sort, how the frozen
/// column collection validates its ids, and row identities.
/// </summary>
public class TableModelTests
{
    [Fact]
    public void ARowIdentityFromAnIndexIsItsInvariantNumber()
    {
        Assert.Equal("42", RowIdentity.UnstableIndex(42).Key);
        Assert.Equal("42", RowIdentity.UnstableIndex(42).ToString());
        Assert.Equal(RowIdentity.UnstableIndex(7), new RowIdentity("7"));
    }

    [Fact]
    public void AColumnStartsVisibleUnpinnedAndSharingTheFreeWidth()
    {
        var column = TableColumn<int>.Of("Title", static row => row);

        Assert.True(column.InitiallyVisible);
        Assert.Equal(TablePin.None, column.Pin);
        Assert.Equal(0, column.AlignContentX);
        Assert.Equal(TableLengthKind.Star, column.Width.Kind);
        Assert.Equal(48f, column.MinWidth);
        Assert.Equal(float.PositiveInfinity, column.MaxWidth);
        Assert.Null(column.Renderer);
    }

    [Fact]
    public void TheFactoryAndTheConstructorWithInitializersBuildTheSameColumn()
    {
        var made = TableColumn<int>.Of("Size", static row => row, id: "bytes", width: 90f, minWidth: 30f,
            maxWidth: 200f, alignContentX: 1, pin: TablePin.Left, visible: false);
        var built = new TableColumn<int, int>("Size", static row => row)
        {
            Id = "bytes",
            Width = 90f,
            MinWidth = 30f,
            MaxWidth = 200f,
            AlignContentX = 1,
            Pin = TablePin.Left,
            InitiallyVisible = false
        };

        Assert.Equal(built.Id, made.Id);
        Assert.Equal(built.Width, made.Width);
        Assert.Equal(built.MinWidth, made.MinWidth);
        Assert.Equal(built.MaxWidth, made.MaxWidth);
        Assert.Equal(built.AlignContentX, made.AlignContentX);
        Assert.Equal(built.Pin, made.Pin);
        Assert.Equal(built.InitiallyVisible, made.InitiallyVisible);
    }

    [Fact]
    public void AColumnNeedsATitleAndAValueReader()
    {
        Assert.Throws<ArgumentNullException>(static () => TableColumn<int>.Of(null!, static row => row));
        Assert.Throws<ArgumentNullException>(static () => TableColumn<int>.Of<int>("T", null!));
        Assert.Equal("", TableColumn<int>.Of("", static row => row, id: "e").Title);
    }

    [Theory]
    [InlineData("Version", "version")]
    [InlineData("File Name", "filename")]
    [InlineData("Size (KB)", "sizekb")]
    [InlineData("  Last-Modified  ", "lastmodified")]
    [InlineData("URL", "url")]
    [InlineData("Col 2", "col2")]
    [InlineData("Größe", "größe")]
    public void AnIdLeftOutIsTheTitleInLowerCaseWithOnlyLettersAndDigits(string title, string expected)
    {
        Assert.Equal(expected, TableColumn<int>.Of(title, static row => row).Id);
    }

    [Fact]
    public void AnExplicitIdWinsOverTheTitleAndSurvivesRetitling()
    {
        var english = TableColumn<int>.Of("Name", static row => row, id: "name");
        var german = TableColumn<int>.Of("Bezeichnung", static row => row, id: "name");

        Assert.Equal("name", english.Id);
        Assert.Equal(english.Id, german.Id);
        Assert.NotEqual(TableColumn<int>.Of("Name", static row => row).Id,
            TableColumn<int>.Of("Bezeichnung", static row => row).Id);
    }

    [Fact]
    public void TheIdCanBeSetThroughAnInitializerAndNeverPositionally()
    {
        var column = new TableColumn<int, int>("Title", static row => row) { Id = "custom" };

        Assert.Equal("custom", column.Id);
        Assert.DoesNotContain(typeof(TableColumn<int, int>).GetConstructors().Single().GetParameters(),
            static parameter => parameter.Name == "id");
    }

    [Fact]
    public void ColumnsAreComparedByInstanceNotByIdOrTitle()
    {
        var first = TableColumn<int>.Of("Same", static row => row);
        var second = TableColumn<int>.Of("Same", static row => row);

        Assert.Equal(first.Id, second.Id);
        Assert.NotSame(first, second);
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void AValueTypeStringAndNullableAreSortableByTheirOwnOrder()
    {
        Assert.True(TableColumn<Row>.Of("A", static row => row.Number).Sortable);
        Assert.True(TableColumn<Row>.Of("B", static row => row.Name).Sortable);
        Assert.True(TableColumn<Row>.Of("C", static row => row.Maybe).Sortable);
        Assert.True(TableColumn<Row>.Of("D", static row => row.When).Sortable);
        Assert.True(TableColumn<Row>.Of("E", static row => row.Kind).Sortable);
    }

    [Fact]
    public void ATypeWithNoNaturalOrderIsOnlySortableWithAComparer()
    {
        Assert.False(TableColumn<Row>.Of("Opaque", static row => row.Opaque).Sortable);
        Assert.False(TableColumn<Row>.Row("Row").Sortable);
        Assert.False(TableColumn<Row>.Of("Object", static row => (object)row.Name).Sortable);

        var withComparer = TableColumn<Row>.Of("Opaque", static row => row.Opaque,
            comparer: Comparer<Opaque>.Create(static (a, b) => a.Weight.CompareTo(b.Weight)));
        var rowWithComparer = TableColumn<Row>.Row("Row",
            comparer: Comparer<Row>.Create(static (a, b) => a.Number.CompareTo(b.Number)));

        Assert.True(withComparer.Sortable);
        Assert.True(rowWithComparer.Sortable);
    }

    [Fact]
    public void ComparingUsesTheValueTypesOrderTheGivenComparerOrRefusesWhenNotSortable()
    {
        var byNumber = TableColumn<Row>.Of("Number", static row => row.Number);
        var byWeight = TableColumn<Row>.Of("Weight", static row => row.Opaque,
            comparer: Comparer<Opaque>.Create(static (a, b) => b.Weight.CompareTo(a.Weight)));
        var opaque = TableColumn<Row>.Of("Opaque", static row => row.Opaque);
        var low = new Row(1, "b", null, default, Kind.Beta, new Opaque(5));
        var high = new Row(2, "a", null, default, Kind.Alpha, new Opaque(9));

        Assert.True(byNumber.Compare(low, high) < 0);
        Assert.True(byWeight.Compare(low, high) > 0, "the given comparer, not the default, decides");
        Assert.Equal(0, byNumber.Compare(low, low));
        Assert.Throws<InvalidOperationException>(() => opaque.Compare(low, high));
    }

    [Fact]
    public void AValueIsReadFromTheRowByTheColumn()
    {
        var column = TableColumn<Row>.Of("Name", static row => row.Name);
        var row = new Row(1, "widget", null, default, Kind.Alpha, new Opaque(1));

        Assert.Equal("widget", column.Value(row));
        Assert.Same(row, TableColumn<Row>.Row("Row").Value(row));
    }

    [Fact]
    public void TheColumnsKeepTheirOrderAndFreezeIt()
    {
        var a = TableColumn<int>.Of("A", static row => row);
        var b = TableColumn<int>.Of("B", static row => row, visible: false);
        var c = TableColumn<int>.Of("C", static row => row);
        var list = new List<TableColumn<int>> { a, b, c };

        var columns = new TableColumns<int>(list);
        list.Clear();

        Assert.Equal(3, columns.Count);
        Assert.Same(a, columns[0]);
        Assert.Same(c, columns[2]);
        Assert.Equal([a, b, c], columns);
    }

    [Fact]
    public void AColumnWithNoTitleTextGetsItsPositionAsAnIdInsteadOfFailing()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row),
            TableColumn<int>.Of("", static row => row),
            TableColumn<int>.Of("—", static row => row),
            TableColumn<int>.Of("", static row => row, id: "share")
        ]);

        Assert.Equal(["a", "column2", "column3", "share"], columns.Select(static column => column.Id));
    }

    [Fact]
    public void ThePositionalIdOnlyAppliesWhereTheTitleGivesNone()
    {
        var titled = TableColumn<int>.Of("Name", static row => row);
        var untitled = TableColumn<int>.Of("", static row => row);

        _ = new TableColumns<int>([untitled, titled]);

        Assert.Equal("name", titled.Id);
        Assert.Equal("column1", untitled.Id);
    }

    [Fact]
    public void ACollisionWithAPositionalIdIsStillRejectedWithAClearMessage()
    {
        var error = Assert.Throws<ArgumentException>(static () => new TableColumns<int>(
        [
            TableColumn<int>.Of("", static row => row),
            TableColumn<int>.Of("Column 1", static row => row)
        ]));

        Assert.Contains("'column1'", error.Message);
        Assert.Contains("Id = ", error.Message);
    }

    [Fact]
    public void AHeaderCanBeSetThroughTheFactoryOrAnInitializerAndDefaultsToTheTitle()
    {
        Content icon = WidgetIcons.ChevronUp;
        var made = TableColumn<int>.Of("", static row => row, header: icon);
        var built = new TableColumn<int, int>("", static row => row) { Header = icon };

        Assert.Equal(WidgetIcons.ChevronUp, made.Header.Label);
        Assert.Equal(WidgetIcons.ChevronUp, built.Header.Label);
        Assert.True(TableColumn<int>.Of("Plain", static row => row).Header.IsEmpty);
        Assert.Equal(WidgetIcons.ChevronUp, TableColumn<int>.Row("Row", header: icon).Header.Label);
    }

    [Fact]
    public void TheFactoriesAndTheInitializerAgreeOnAlignment()
    {
        var made = TableColumn<int>.Of("A", static row => row);
        var row = TableColumn<int>.Row("B");
        var built = new TableColumn<int, int>("C", static value => value);

        Assert.Equal(built.AlignContentX, made.AlignContentX);
        Assert.Equal(built.AlignContentY, made.AlignContentY);
        Assert.Equal(built.AlignContentY, row.AlignContentY);
        Assert.Equal(0.5f, built.AlignContentY);
    }

    [Fact]
    public void AnEmptyExplicitIdIsRejected()
    {
        var column = new TableColumn<int, int>("Title", static row => row) { Id = "" };

        Assert.Throws<ArgumentException>(() => new TableColumns<int>([column]));
    }

    [Fact]
    public void ADuplicateIdIsRejectedAtConstructionNamingBothColumns()
    {
        var error = Assert.Throws<ArgumentException>(static () => new TableColumns<int>(
        [
            TableColumn<int>.Of("First", static row => row, id: "dup"),
            TableColumn<int>.Of("Second", static row => row, id: "dup")
        ]));

        Assert.Contains("'First'", error.Message);
        Assert.Contains("'Second'", error.Message);
        Assert.Contains("'dup'", error.Message);
        Assert.Contains("Id = ", error.Message);
        Assert.Contains("unique titles", error.Message);
    }

    [Fact]
    public void TwoTitlesThatDeriveTheSameIdAreRejectedNotRenamedBehindTheCallersBack()
    {
        var error = Assert.Throws<ArgumentException>(static () => new TableColumns<int>(
        [
            TableColumn<int>.Of("File Name", static row => row),
            TableColumn<int>.Of("file-name", static row => row)
        ]));

        Assert.Contains("'filename'", error.Message);
    }

    [Fact]
    public void ExplicitIdsLetColumnsShareATitle()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("Size", static row => row, id: "before"),
            TableColumn<int>.Of("Size", static row => row, id: "after")
        ]);

        Assert.Equal(["before", "after"], columns.Select(static column => column.Id));
    }

    [Fact]
    public void ANullColumnOrListIsRejected()
    {
        Assert.Throws<ArgumentNullException>(static () => new TableColumns<int>(null!));
        Assert.Throws<ArgumentException>(static () => new TableColumns<int>([null!]));
    }

    [Fact]
    public void TheIdLookupIsNotPartOfThePublicApi()
    {
        var members = typeof(TableColumns<int>).GetMembers().Select(static member => member.Name);

        Assert.DoesNotContain("Find", members);
        Assert.DoesNotContain("Set", members);
        Assert.DoesNotContain("Contains", members);
    }

    enum Kind
    {
        Alpha,
        Beta
    }

    /// <summary>A value with no natural order.</summary>
    readonly record struct Opaque(int Weight);

    sealed record Row(int Number, string Name, int? Maybe, DateTime When, Kind Kind, Opaque Opaque);
}

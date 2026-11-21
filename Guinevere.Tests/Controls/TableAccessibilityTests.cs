using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers what a table tells assistive technology: the table, its column headers, and the rows and cells in view
/// with names, roles, parents and state, and the announcements of a new selection or sort.
/// </summary>
public class TableAccessibilityTests : TableTestBase
{
    [Fact]
    public void TheTreeHoldsTheTableItsHeadersAndTheRowsInView()
    {
        var f = new Fixture(rows: 300);
        f.Frames(3);

        var table = f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.Table);
        Assert.Equal("Files", table.Name);
        Assert.Null(table.ParentId);
        Assert.Contains("300 rows, 2 columns", table.Description);

        var headers = f.Tree.Elements.Values.Where(static element => element.Role == ControlRole.ColumnHeader).ToArray();
        Assert.Equal(["Name", "Downloads"], headers.Select(static header => header.Name).Order().Reverse());
        Assert.All(headers, header => Assert.Equal(table.Id, header.ParentId));

        var rows = f.Tree.Elements.Values.Where(static element => element.Role == ControlRole.Row).ToArray();
        Assert.InRange(rows.Length, 5, 40);
        Assert.All(rows, row => Assert.Equal(table.Id, row.ParentId));
    }

    [Fact]
    public void ARowIsNamedByItsFirstCellAndItsCellsAreItsChildren()
    {
        var f = new Fixture();
        f.Frames(3);

        var row = f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.Row && element.Key == "b.zip");
        Assert.Equal("b.zip", row.Name);
        Assert.Equal("Row 2 of 3", row.Description);

        var cells = f.Tree.Elements.Values.Where(element => element.ParentId == row.Id).ToArray();
        Assert.Equal(["Downloads", "Name"], cells.Select(static cell => cell.Description).Order());
        Assert.Equal(["5", "b.zip"], cells.Select(static cell => cell.Name).Order());
        Assert.All(cells, static cell => Assert.Equal(ControlRole.Cell, cell.Role));
        Assert.All(cells, static cell => Assert.True(cell.Bounds.W > 0f));
    }

    [Fact]
    public void SelectionAndKeyboardFocusAreStateOnTheRow()
    {
        var f = new Fixture();
        f.Frames(2);
        f.Harness.Click(f.Draw, f.RowPoint("c.zip"));
        f.Frames(2);

        var row = f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.Row && element.Key == "c.zip");
        Assert.True(row.State.HasFlag(ControlVisualState.Selected));
        Assert.True(row.State.HasFlag(ControlVisualState.Focused));
        var other = f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.Row && element.Key == "a.zip");
        Assert.False(other.State.HasFlag(ControlVisualState.Selected));
    }

    [Fact]
    public void ARowWithDetailsSaysWhetherItIsExpanded()
    {
        var f = new Fixture(details: true);
        f.Frames(3);
        f.State.Expansion.SetExpanded(new RowIdentity("a.zip"), true);
        f.Frames(3);

        Assert.True(f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.Row && element.Key == "a.zip").Expanded);
        Assert.False(f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.Row && element.Key == "b.zip").Expanded);
    }

    [Fact]
    public void ASortedColumnHeaderSaysSo()
    {
        var f = new Fixture();
        f.Frames(2);
        f.State.SortBy(f.Source, f.Columns[0]);
        f.Frames(3);

        var header = f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.ColumnHeader && element.Key == "name");
        Assert.Equal("sorted ascending", header.Description);
        Assert.Equal("", f.Tree.Elements.Values
            .Single(static element => element.Role == ControlRole.ColumnHeader && element.Key == "downloads").Description);
    }

    [Fact]
    public void ANewSelectionAndANewSortAreAnnouncedOnce()
    {
        var f = new Fixture();
        f.Frames(2);
        f.Harness.Click(f.Draw, f.RowPoint("a.zip"));
        f.Frames(3);

        Assert.Equal(1, f.Tree.Announcements.Count(static note => note.Message == "1 row selected"));
        var scope = f.Tree.Elements.Values.Single(static element => element.Role == ControlRole.Table).Id;
        Assert.All(f.Tree.Announcements, note => Assert.Equal(scope, note.Scope));

        f.State.SortBy(f.Source, f.Columns[1]);
        f.Frames(3);
        Assert.Equal(1, f.Tree.Announcements.Count(static note => note.Message == "Sorted by Downloads, ascending"));

        f.State.Selection.Clear();
        f.Frames(2);
        Assert.Contains(f.Tree.Announcements, static note => note.Message == "Selection cleared");
    }

    [Fact]
    public void SeveralSelectedRowsAreCounted()
    {
        var f = new Fixture();
        f.Frames(2);
        f.State.Selection.Set([new RowIdentity("a.zip"), new RowIdentity("b.zip")]);
        f.Frames(2);

        Assert.Contains(f.Tree.Announcements, static note => note.Message == "2 rows selected");
    }

    [Fact]
    public void WithoutATreeSinkNothingIsPublishedAndNothingBreaks()
    {
        var f = new Fixture(register: false);

        f.Frames(3);

        Assert.Empty(f.Tree.Elements);
    }

    sealed class Fixture
    {
        public Fixture(int rows = 3, bool details = false, bool register = true)
        {
            Columns = new TableColumns<Item>(
            [
                TableColumn<Item>.Of("Name", static item => item.FileName, id: "name", width: 150f),
                TableColumn<Item>.Of("Downloads", static item => item.Downloads, id: "downloads", width: 100f)
            ]);
            var items = rows == 3
                ? [new Item("1", "a.zip", 1), new Item("2", "b.zip", 5), new Item("3", "c.zip", 9)]
                : Enumerable.Range(0, rows).Select(static i => new Item("v", $"f{i}.zip", i)).ToArray();
            Source = new ListTableSource<Item>(items, static item => item.FileName);
            Options = new TableOptions<Item>
            {
                AccessibleName = "Files",
                Selection = TableSelectionMode.Multiple,
                RowDetails = details ? new Details() : null
            };
            Harness = new FrameHarness(Width, Height);
            if (register)
            {
                Harness.Gui.Platform.Register<IAccessibilityTreeSink>(Tree);
            }

            State = new TableState();
            Draw = gui => gui.Table(State, Source, Columns, Options);
        }

        public TableColumns<Item> Columns { get; }

        public ListTableSource<Item> Source { get; }

        public TableOptions<Item> Options { get; }

        public FrameHarness Harness { get; }

        public TableState State { get; }

        public Action<Gui> Draw { get; }

        public FakeTree Tree { get; } = new();

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Harness.Frame(Draw);
            }
        }

        public Vector2 RowPoint(string key)
        {
            var node = Harness.Gui.RootNode!.FindChildById($"table/row{key}")!;
            return new Vector2(node.Rect.X + 20f, node.Rect.Y + (node.Rect.H / 2f));
        }
    }

    sealed class Details : ITableRowDetails<Item>
    {
        public bool HasDetails(in TableRowContext<Item> row) => true;

        public float GetHeight(in TableRowContext<Item> row) => 30f;

        public void Render(Gui gui, in TableRowContext<Item> row, float width, float height)
        {
        }
    }

    sealed class FakeTree : IAccessibilityTreeSink
    {
        public Dictionary<string, AccessibilityElement> Elements { get; } = [];

        public List<(string Scope, string Message)> Announcements { get; } = [];

        public void PublishElement(AccessibilityElement element) => Elements[element.Id] = element;

        public void Announce(string scopeId, string message, string? elementId = null) =>
            Announcements.Add((scopeId, message));
    }
}

using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers rows as the user meets them: selecting by pointer and key, dragging across rows, activation, the row menu,
/// pinned cells, and a header that sorts, all through full frames.
/// </summary>
public class TableRowInteractionTests : TableTestBase
{
    [Fact]
    public void ClicksDoNothingWhileSelectionIsOff()
    {
        var f = new Fixture(TableSelectionMode.None);

        f.ClickRow(2);

        Assert.Equal(0, f.State.Selection.Count);
    }

    [Fact]
    public void AClickSelectsTheRowAndMarksItSelected()
    {
        var f = new Fixture(TableSelectionMode.Multiple);

        f.ClickRow(2);

        Assert.Equal(["2"], f.SelectedKeys());
        Assert.Contains("selected", f.Modifiers(2));
        Assert.DoesNotContain("selected", f.Modifiers(3));
        Assert.Contains("current", f.Modifiers(2));
    }

    [Fact]
    public void AClickKeepsSelectionAcrossAScroll()
    {
        var f = new Fixture(TableSelectionMode.Multiple, rows: 500);
        f.ClickRow(2);

        f.Harness.Gui.ScrollBy("table/body", new Vector2(0f, 2000f));
        f.Frames(3);
        f.Harness.Gui.ScrollBy("table/body", new Vector2(0f, -2000f));
        f.Frames(3);

        Assert.Contains("selected", f.Modifiers(2));
    }

    [Fact]
    public void ControlAndShiftClicksToggleAndExtend()
    {
        var f = new Fixture(TableSelectionMode.Multiple);
        f.ClickRow(1);

        f.Harness.Input.PressKey(KeyboardKey.LeftControl);
        f.ClickRow(4);
        f.Harness.Input.ReleaseKey(KeyboardKey.LeftControl);
        Assert.Equal(["1", "4"], f.SelectedKeys());

        f.Harness.Input.PressKey(KeyboardKey.LeftShift);
        f.ClickRow(6);
        f.Harness.Input.ReleaseKey(KeyboardKey.LeftShift);
        Assert.Equal(["4", "5", "6"], f.SelectedKeys());
    }

    [Fact]
    public void DraggingAcrossRowsSelectsTheRange()
    {
        var f = new Fixture(TableSelectionMode.Multiple);
        f.Frames(2);
        var from = f.RowPoint(1);

        f.Harness.Input.MoveTo(from);
        f.Harness.Input.PressButton();
        f.Frames(1);
        f.Harness.Input.MoveTo(f.RowPoint(3));
        f.Frames(1);
        f.Harness.Input.MoveTo(f.RowPoint(5));
        f.Frames(1);

        Assert.Equal(["1", "2", "3", "4", "5"], f.SelectedKeys());

        f.Harness.Input.ReleaseButton();
        f.Frames(1);
        f.Harness.Input.MoveTo(f.RowPoint(8));
        f.Frames(2);
        Assert.Equal(["1", "2", "3", "4", "5"], f.SelectedKeys());
        Assert.False(f.State.DragSelecting);
    }

    [Fact]
    public void SingleModeDoesNotDragSelect()
    {
        var f = new Fixture(TableSelectionMode.Single);
        f.Frames(2);

        f.Harness.Input.MoveTo(f.RowPoint(1));
        f.Harness.Input.PressButton();
        f.Frames(1);
        f.Harness.Input.MoveTo(f.RowPoint(4));
        f.Frames(2);

        Assert.Equal(["1"], f.SelectedKeys());
    }

    [Fact]
    public void ADoubleClickActivatesTheRow()
    {
        var f = new Fixture(TableSelectionMode.Single);
        f.Frames(2);

        f.ClickRow(3);
        f.ClickRow(3);

        Assert.Equal([3], f.Activated);
    }

    [Fact]
    public void ClickingAPinnedCellSelectsItsRowToo()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("Pinned", static row => row, id: "pinned", width: 80f, pin: TablePin.Left),
            TableColumn<int>.Of("Other", static row => row, id: "other", width: 300f)
        ]);
        var f = new Fixture(TableSelectionMode.Multiple, columns: columns);
        f.Frames(2);

        var strip = f.Harness.Gui.RootNode!.FindChildById("table/row2/pinned-left")!;
        f.Harness.Click(f.Draw, FrameHarness.Center(strip));

        Assert.Equal(["2"], f.SelectedKeys());
    }

    [Fact]
    public void TheKeyboardMovesSelectsAndScrollsTheRow()
    {
        var f = new Fixture(TableSelectionMode.Multiple, rows: 200);
        f.ClickRow(0);

        f.Key(KeyboardKey.Down);
        Assert.Equal(["1"], f.SelectedKeys());
        Assert.Equal(1, f.State.Selection.CurrentIndex);

        f.Key(KeyboardKey.PageDown);
        Assert.True(f.State.Selection.CurrentIndex > 5);

        f.Key(KeyboardKey.End);
        Assert.Equal(199, f.State.Selection.CurrentIndex);
        f.Key(KeyboardKey.Down);
        Assert.Equal(199, f.State.Selection.CurrentIndex);

        f.Key(KeyboardKey.Home);
        Assert.Equal(0, f.State.Selection.CurrentIndex);
        f.Key(KeyboardKey.Up);
        Assert.Equal(0, f.State.Selection.CurrentIndex);
        f.Key(KeyboardKey.PageUp);
        Assert.Equal(0, f.State.Selection.CurrentIndex);
    }

    [Fact]
    public void AnArrowFromNoRowStartsAtTheFirst()
    {
        var f = new Fixture(TableSelectionMode.Multiple);
        f.Frames(2);
        f.Harness.Gui.RequestFocus("table/body");
        f.Frames(1);

        f.Key(KeyboardKey.Down);

        Assert.Equal(["0"], f.SelectedKeys());
    }

    [Fact]
    public void SpaceTogglesEnterActivatesAndControlAEverySelects()
    {
        var f = new Fixture(TableSelectionMode.Multiple, rows: 12);
        f.ClickRow(2);

        f.Key(KeyboardKey.Space);
        Assert.Equal(0, f.State.Selection.Count);
        f.Key(KeyboardKey.Space);
        Assert.Equal(["2"], f.SelectedKeys());

        f.Key(KeyboardKey.Enter);
        Assert.Equal([2], f.Activated);

        f.Harness.Input.PressKey(KeyboardKey.LeftControl);
        f.Key(KeyboardKey.A);
        f.Harness.Input.ReleaseKey(KeyboardKey.LeftControl);
        Assert.Equal(12, f.State.Selection.Count);
    }

    [Fact]
    public void ControlAIsIgnoredInSingleMode()
    {
        var f = new Fixture(TableSelectionMode.Single, rows: 12);
        f.ClickRow(2);

        f.Harness.Input.PressKey(KeyboardKey.LeftControl);
        f.Key(KeyboardKey.A);
        f.Harness.Input.ReleaseKey(KeyboardKey.LeftControl);

        Assert.Equal(1, f.State.Selection.Count);
    }

    [Fact]
    public void KeysAreIgnoredWithoutFocus()
    {
        var f = new Fixture(TableSelectionMode.Multiple);
        f.Frames(2);

        f.Key(KeyboardKey.Down);

        Assert.Equal(0, f.State.Selection.Count);
    }

    [Fact]
    public void ARightClickSelectsTheRowAndBuildsItsMenu()
    {
        var f = new Fixture(TableSelectionMode.Multiple, menu: true);
        f.ClickRow(1);

        f.Harness.Click(f.Draw, f.RowPoint(3), MouseButton.Right);
        f.Frames(2);

        Assert.Equal(["3"], f.SelectedKeys());
        Assert.True(f.State.RowMenuOpen);
        Assert.Contains(3, f.MenuRows);
    }

    [Fact]
    public void ARightClickInsideTheSelectionKeepsIt()
    {
        var f = new Fixture(TableSelectionMode.Multiple, menu: true);
        f.ClickRow(1);
        f.Harness.Input.PressKey(KeyboardKey.LeftShift);
        f.ClickRow(3);
        f.Harness.Input.ReleaseKey(KeyboardKey.LeftShift);

        f.Harness.Click(f.Draw, f.RowPoint(2), MouseButton.Right);

        Assert.Equal(["1", "2", "3"], f.SelectedKeys());
    }

    [Fact]
    public void AMenuForARowThatMovedAwayCloses()
    {
        var f = new Fixture(TableSelectionMode.Multiple, menu: true);
        f.Frames(2);
        f.Harness.Click(f.Draw, f.RowPoint(3), MouseButton.Right);
        f.Frames(1);

        f.State.MenuKey = "not-there";
        f.Frames(2);

        Assert.False(f.State.RowMenuOpen);
    }

    [Fact]
    public void NoRowMenuMeansARightClickOpensNothing()
    {
        var f = new Fixture(TableSelectionMode.Multiple);
        f.Frames(2);

        f.Harness.Click(f.Draw, f.RowPoint(3), MouseButton.Right);

        Assert.False(f.State.RowMenuOpen);
        Assert.Equal(0, f.State.Selection.Count);
    }

    [Fact]
    public void AClickOnASortableHeaderSortsAndAnotherFlipsIt()
    {
        var s = new SortFixture();
        s.Frames(2);
        Assert.Equal(["b.zip", "c.zip", "a.zip"], s.Order());

        s.ClickHeader("name");
        s.Frames(3);
        Assert.Equal(["a.zip", "b.zip", "c.zip"], s.Order());
        Assert.Equal(TableSortDirection.Ascending, s.State.Sort.Primary!.Value.Direction);

        s.ClickHeader("name");
        s.Frames(3);
        Assert.Equal(["c.zip", "b.zip", "a.zip"], s.Order());
    }

    [Fact]
    public void EnterOnAFocusedHeaderSorts()
    {
        var s = new SortFixture();
        s.Frames(2);
        s.ClickHeader("downloads");
        s.Frames(2);

        Assert.Equal("downloads", s.State.Sort.Primary!.Value.Column.Id);
        s.Harness.Input.PressKey(KeyboardKey.Enter);
        s.Frames(1);
        s.Harness.Input.ReleaseKey(KeyboardKey.Enter);
        s.Frames(2);

        Assert.Equal(TableSortDirection.Descending, s.State.Sort.Primary!.Value.Direction);
    }

    [Fact]
    public void ADragThatMovedTheHeaderDoesNotSort()
    {
        var s = new SortFixture();
        s.Frames(2);
        var cell = s.Harness.Gui.RootNode!.FindChildById("table/header/name")!;
        var from = FrameHarness.Center(cell);

        s.Harness.Input.MoveTo(from);
        s.Harness.Input.PressButton();
        s.Frames(1);
        s.Harness.Input.MoveTo(from + new Vector2(30f, 0f));
        s.Frames(1);
        s.Harness.Input.ReleaseButton();
        s.Frames(2);

        Assert.Null(s.State.Sort.Primary);
    }

    [Fact]
    public void AColumnThatCannotSortAndASourceThatCannotSortAreLeftAlone()
    {
        var plain = new Fixture(TableSelectionMode.None);
        plain.Frames(2);
        plain.Harness.Click(plain.Draw, FrameHarness.Center(plain.Harness.Gui.RootNode!.FindChildById("table/header/c0")!));
        Assert.Null(plain.State.Sort.Primary);

        var s = new SortFixture();
        s.Frames(2);
        var actions = s.Harness.Gui.RootNode!.FindChildById("table/header/actions")!;
        s.Harness.Click(s.Draw, FrameHarness.Center(actions));
        Assert.Null(s.State.Sort.Primary);
    }

    sealed class Fixture
    {
        readonly TableColumns<int> _columns;
        readonly TableOptions _options;
        readonly GridSource _source;

        public Fixture(TableSelectionMode mode, int rows = 40, TableColumns<int>? columns = null, bool menu = false)
        {
            _columns = columns ?? Fixed(3, 100f);
            _source = new GridSource(rows);
            _options = new TableOptions<int>
            {
                Selection = mode,
                RowActivated = row => Activated.Add(row.Index),
                RowMenu = menu ? (_, row) => MenuRows.Add(row.Index) : null
            };
            Harness = new FrameHarness(Width, Height);
            State = new TableState();
            Draw = gui => gui.Table(State, _source, _columns, _options);
        }

        public FrameHarness Harness { get; }

        public TableState State { get; }

        public Action<Gui> Draw { get; }

        public List<int> Activated { get; } = [];

        public List<int> MenuRows { get; } = [];

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Harness.Frame(Draw);
            }
        }

        public Vector2 RowPoint(int row)
        {
            var node = Harness.Gui.RootNode!.FindChildById($"table/row{row}")!;
            return new Vector2(node.Rect.X + 20f, node.Rect.Y + (node.Rect.H / 2f));
        }

        public void ClickRow(int row)
        {
            Frames(2);
            Harness.Click(Draw, RowPoint(row));
        }

        public void Key(KeyboardKey key)
        {
            Harness.Input.PressKey(key);
            Frames(1);
            Harness.Input.ReleaseKey(key);
            Frames(2);
        }

        public string[] SelectedKeys() => [.. State.Selection.ToArray().Select(static id => id.Key)
            .OrderBy(static key => int.Parse(key))];

        public IReadOnlyList<string> Modifiers(int row) =>
            Harness.Gui.RootNode!.FindChildById($"table/row{row}")!.StyleTarget?.Modifiers ?? [];
    }

    sealed class SortFixture
    {
        readonly TableColumns<Item> _columns;
        readonly ListTableSource<Item> _source;

        public SortFixture()
        {
            _columns = new TableColumns<Item>(
            [
                TableColumn<Item>.Of("Name", static item => item.FileName, id: "name", width: 120f),
                TableColumn<Item>.Of("Downloads", static item => item.Downloads, id: "downloads", width: 120f),
                TableColumn<Item>.Row("Actions", id: "actions", width: 100f, renderer: new ItemRenderer(),
                    comparer: null)
            ]);
            _source = new ListTableSource<Item>(
                [new Item("1", "b.zip", 5), new Item("2", "c.zip", 9), new Item("3", "a.zip", 1)],
                static item => item.FileName);
            Harness = new FrameHarness(Width, Height);
            State = new TableState();
            Draw = gui => gui.Table(State, _source, _columns);
        }

        public FrameHarness Harness { get; }

        public TableState State { get; }

        public Action<Gui> Draw { get; }

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Harness.Frame(Draw);
            }
        }

        public void ClickHeader(string column) =>
            Harness.Click(Draw, FrameHarness.Center(Harness.Gui.RootNode!.FindChildById($"table/header/{column}")!));

        public string[] Order() =>
        [
            .. new[] { "a.zip", "b.zip", "c.zip" }
                .Select(key => (Key: key, Y: Harness.Gui.RootNode!.FindChildById($"table/row{key}")!.Rect.Y))
                .OrderBy(static row => row.Y)
                .Select(static row => row.Key)
        ];
    }
}

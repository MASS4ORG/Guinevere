using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the table as a v7 widget: metrics read from the sheet, column classes, <c>enabled</c>, and the
/// <c>Changed</c> and <c>Clicked</c> signals of the node it returns.
/// </summary>
public class TableWidgetTests : TableTestBase
{
    [Fact]
    public void DefaultMetricsMatchTheLongStandingDefaults()
    {
        var f = new Fixture();
        f.Frames(3);

        var first = f.Node("table/row0");
        var second = f.Node("table/row1");

        Assert.Equal(RowHeight, second.Rect.Y - first.Rect.Y, 0.01f);
        Assert.Equal(RowHeight, first.Rect.H, 0.01f);
        Assert.Equal(HeaderHeight, f.Node("table/header").Rect.H, 0.01f);
        var cell = f.Node("table/row0/name");
        Assert.Equal(cell.Rect.X + CellPadding, cell.Children[0].Rect.X, 0.5f);
    }

    [Fact]
    public void ASheetRowHeightChangesThePitchAndTheVirtualWindow()
    {
        var plain = new Fixture();
        plain.Frames(3);
        var tall = new Fixture("table-row { height = 30; }");
        tall.Frames(3);

        var pitch = tall.Node("table/row1").Rect.Y - tall.Node("table/row0").Rect.Y;

        Assert.Equal(30f, pitch, 0.01f);
        Assert.Equal(30f, tall.Node("table/row0").Rect.H, 0.01f);
        Assert.True(RowIndices(tall.Harness.Gui).Count < RowIndices(plain.Harness.Gui).Count);
    }

    [Fact]
    public void ASheetHeaderHeightAndCellInsetAreRead()
    {
        var f = new Fixture("table-header { height = 40; } table-cell { inset = 14; }");
        f.Frames(3);

        var cell = f.Node("table/row0/name");
        Assert.Equal(40f, f.Node("table/header").Rect.H, 0.01f);
        Assert.Equal(cell.Rect.X + 14f, cell.Children[0].Rect.X, 0.5f);
    }

    [Fact]
    public void AColumnClassAlignsTheHeaderAndTheCells()
    {
        var f = new Fixture();
        f.Frames(3);

        var headerCell = f.Node("table/header/downloads");
        var headerText = HeaderContent(f.Harness.Gui, "downloads")[0];
        var cell = f.Node("table/row0/downloads");
        var text = cell.Children[0];

        Assert.Equal(headerCell.Rect.X + headerCell.Rect.W - CellPadding, headerText.Rect.X + headerText.Rect.W, 1f);
        Assert.Equal(cell.Rect.X + cell.Rect.W - CellPadding, text.Rect.X + text.Rect.W, 1f);
        var leading = f.Node("table/row0/name").Children[0];
        Assert.Equal(f.Node("table/row0/name").Rect.X + CellPadding, leading.Rect.X, 1f);
    }

    [Fact]
    public void ACellStaysVerticallyCentered()
    {
        var f = new Fixture();
        f.Frames(3);

        var cell = f.Node("table/row0/name");
        var text = cell.Children[0];

        Assert.Equal(cell.Rect.Y + (cell.Rect.H / 2f), text.Rect.Y + (text.Rect.H / 2f), 1f);
    }

    [Fact]
    public void ADisabledTableIgnoresARowClickAndAHeaderClick()
    {
        var f = new Fixture(enabled: false);
        f.Frames(3);

        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/row2")));
        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/header/name")));
        f.Frames(2);

        Assert.Empty(f.State.Selection.ToArray());
        Assert.Equal(0, f.State.Sort.Version);
        Assert.Equal(0, f.Changes);
    }

    [Fact]
    public void ADisabledTableMatchesTheDisabledState()
    {
        var f = new Fixture(enabled: false);
        f.Frames(3);

        Assert.True(f.Node("tbl").StyleTarget!.Value.State.HasFlag(StyleState.Disabled));
    }

    [Fact]
    public void AnEnabledTableSelectsAndSortsFromTheSameClicks()
    {
        var f = new Fixture();
        f.Frames(3);

        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/row2")));
        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/header/name")));

        Assert.Single(f.State.Selection.ToArray());
        Assert.Equal(TableSortDirection.Ascending, f.State.Sort[0].Direction);
    }

    [Fact]
    public void ChangedFiresOnceWhenAClickChangesTheSelection()
    {
        var f = new Fixture();
        f.Frames(3);

        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/row2")));
        f.Frames(3);

        Assert.Equal(1, f.Changes);

        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/row2")));
        f.Frames(3);

        Assert.Equal(1, f.Changes);
    }

    [Fact]
    public void ChangedFiresOnceWhenAHeaderClickChangesTheSort()
    {
        var f = new Fixture();
        f.Frames(3);

        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/header/name")));
        f.Frames(3);

        Assert.Equal(1, f.Changes);
        Assert.Equal(0, f.Clicks);
    }

    [Fact]
    public void ClickedFiresWhenARowIsActivated()
    {
        var f = new Fixture();
        f.Frames(3);

        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/row3")));
        Assert.Equal(0, f.Clicks);

        f.Harness.Click(f.Draw, FrameHarness.Center(f.Node("table/row3")));

        Assert.Equal(1, f.Clicks);
    }

    sealed class Fixture
    {
        readonly TableColumns<Item> _columns;
        readonly ListTableSource<Item> _source;
        readonly TableOptions _options = new() { Selection = TableSelectionMode.Single };
        readonly bool _enabled;

        public Fixture(string? sheet = null, bool enabled = true)
        {
            _enabled = enabled;
            _columns = new TableColumns<Item>(
            [
                TableColumn<Item>.Of("Name", static item => item.FileName, id: "name", width: 140f),
                TableColumn<Item>.Of("Downloads", static item => item.Downloads, id: "downloads", width: 140f,
                    classes: ["end"])
            ]);
            _source = new ListTableSource<Item>(
                [.. Enumerable.Range(0, 60).Select(static i => new Item("1", i.ToString(), i))],
                static item => item.FileName);
            Harness = new FrameHarness(Width, Height);
            if (sheet is not null)
            {
                Harness.Gui.StyleSheets.Add(StyleSheet.Parse(sheet));
            }

            State = new TableState();
            Draw = gui =>
            {
                var node = gui.Table(State, _source, _columns, _options, _enabled, id: "tbl");
                if (gui.Pass == Pass.Pass2Render)
                {
                    Changes += node.Changed ? 1 : 0;
                    Clicks += node.Clicked ? 1 : 0;
                }
            };
        }

        public FrameHarness Harness { get; }

        public TableState State { get; }

        public Action<Gui> Draw { get; }

        public int Changes { get; private set; }

        public int Clicks { get; private set; }

        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Harness.Frame(Draw);
            }
        }

        public LayoutNode Node(string id) => Harness.Gui.RootNode!.FindChildById(id)!;
    }
}

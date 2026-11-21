using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers expandable rows: the expansion state by identity, the offsets that give every row its place below an
/// expanded one, the expander column and the keyboard, and what a sort or a change of data does to expanded rows.
/// </summary>
public class TableDetailsTests : TableTestBase
{
    const float Detail = 50f;

    [Fact]
    public void ExpansionIsKeptByIdentityAndCountsItsChanges()
    {
        var expansion = new TableExpansion();
        var a = RowIdentity.UnstableIndex(1);
        var version = expansion.Version;

        Assert.True(expansion.SetExpanded(a, true, 1));
        Assert.True(expansion.IsExpanded(a));
        Assert.False(expansion.SetExpanded(a, true, 1));
        Assert.True(expansion.SetExpanded(a, true, 4));
        Assert.True(expansion.Version > version);
        Assert.False(expansion.Toggle(a));
        Assert.True(expansion.Toggle(a, 2));
        Assert.True(expansion.SetExpanded(a, false));
        Assert.False(expansion.SetExpanded(a, false));
        expansion.SetExpanded(a, true);
        Assert.True(expansion.Clear());
        Assert.False(expansion.Clear());
        Assert.Equal(0, expansion.Count);
    }

    [Fact]
    public void AnExpandedRowPushesTheRowsBelowItDownByItsDetails()
    {
        var f = new Fixture();
        f.Frames(2);
        var before = f.RowY(1);

        f.State.Expansion.SetExpanded(RowIdentity.UnstableIndex(0), true, 0);
        f.Frames(3);

        Assert.Equal(before + Detail, f.RowY(1), 0.5f);
        var details = f.Harness.Gui.RootNode!.FindChildById("table/row0/details")!;
        Assert.Equal(Detail, details.Rect.H, 0.5f);
        Assert.Equal(f.RowY(0) + 24f, details.Rect.Y, 0.5f);
    }

    [Fact]
    public void TheProviderDrawsInsideItsNodeAndIsToldItsRow()
    {
        var f = new Fixture();
        f.Frames(2);
        f.State.Expansion.SetExpanded(RowIdentity.UnstableIndex(2), true, 2);

        f.Frames(3);

        Assert.Contains(f.Details.Rendered, static row => row.Index == 2 && row.Identity.Key == "2");
        Assert.All(f.Details.Rendered, static row => Assert.Equal(0, row.Index % 2));
    }

    [Fact]
    public void ARowWithoutDetailsDoesNotExpand()
    {
        var f = new Fixture();
        f.Frames(2);
        var before = f.RowY(2);

        f.State.Expansion.SetExpanded(RowIdentity.UnstableIndex(1), true, 1);
        f.Frames(3);

        Assert.Equal(before, f.RowY(2), 0.5f);
        Assert.Null(f.Harness.Gui.RootNode!.FindChildById("table/row1/details"));
    }

    [Fact]
    public void TheScrollableHeightGrowsByTheDetailsAndRowsFarDownKeepTheirPlace()
    {
        var f = new Fixture(rows: 400);
        f.Frames(2);
        f.State.Expansion.SetExpanded(RowIdentity.UnstableIndex(0), true, 0);
        f.State.Expansion.SetExpanded(RowIdentity.UnstableIndex(2), true, 2);
        f.Frames(3);

        Assert.Equal((400 * 24f) + (2 * Detail), f.State.Geometry.Offsets!.Total, 0.01f);
        Assert.Equal((10 * 24f) + (2 * Detail), f.State.Geometry.Offsets.TopOf(10), 0.01f);
        Assert.Equal(2, f.State.Geometry.Offsets.RowAt((2 * 24f) + Detail + 5f));

        f.Harness.Gui.ScrollBy("table/body", new Vector2(0f, 4000f));
        f.Frames(4);
        var shown = RowIndices(f.Harness.Gui);
        Assert.True(shown.Min() > 100);
        Assert.True(shown.Count < 40);
    }

    [Fact]
    public void WithNothingExpandedRowsKeepTheFixedStrideAndNoOffsets()
    {
        var f = new Fixture();
        f.Frames(3);

        Assert.Null(f.State.Geometry.Offsets);
    }

    [Fact]
    public void TheExpanderColumnTogglesTheRowAndOnlyRowsThatHaveDetails()
    {
        var f = new Fixture(expander: true);
        f.Frames(2);

        var chevron = f.Harness.Gui.RootNode!.FindChildById("table/row0/expander")!;
        f.Harness.Click(f.Draw, FrameHarness.Center(chevron));
        f.Frames(2);

        Assert.True(f.State.Expansion.IsExpanded(RowIdentity.UnstableIndex(0)));
        Assert.NotNull(f.Harness.Gui.RootNode.FindChildById("table/row0/details"));

        var plain = f.Harness.Gui.RootNode.FindChildById("table/row1/expander")!;
        Assert.Empty(plain.Children);
    }

    [Fact]
    public void TheExpanderColumnCannotBeSortedOrResized()
    {
        var column = TableColumn<int>.Expander();

        Assert.False(column.Sortable);
        Assert.False(column.Resizable);
        Assert.Equal("expander", column.Id);
        Assert.Equal(TableLength.Pixels(32f), column.Width);
    }

    [Fact]
    public void RightAndLeftExpandAndCollapseTheKeyboardRow()
    {
        var f = new Fixture(selection: true);
        f.Frames(2);
        f.Harness.Click(f.Draw, f.RowPoint(2));
        f.Frames(2);

        f.Key(KeyboardKey.Right);
        Assert.True(f.State.Expansion.IsExpanded(RowIdentity.UnstableIndex(2)));

        f.Key(KeyboardKey.Left);
        Assert.False(f.State.Expansion.IsExpanded(RowIdentity.UnstableIndex(2)));
    }

    [Fact]
    public void ExpandedRowsFollowTheirIdentityThroughASortWhenTheSourceCanLookThemUp()
    {
        var s = new ListFixture();
        s.Frames(2);
        s.State.Expansion.SetExpanded(new RowIdentity("a.zip"), true);
        s.Frames(3);
        Assert.NotNull(s.Harness.Gui.RootNode!.FindChildById("table/rowa.zip/details"));
        var topBefore = s.Harness.Gui.RootNode.FindChildById("table/rowa.zip")!.Rect.Y;

        s.Source.ApplySort([new TableSort(s.Columns[0], TableSortDirection.Descending)]);
        s.Frames(3);

        var details = s.Harness.Gui.RootNode.FindChildById("table/rowa.zip/details");
        Assert.NotNull(details);
        var row = s.Harness.Gui.RootNode.FindChildById("table/rowa.zip")!;
        Assert.NotEqual(topBefore, row.Rect.Y);
        Assert.Equal(row.Rect.Y + 24f, details.Rect.Y, 0.5f);
        Assert.Equal(2, s.State.Geometry.Offsets!.RowAt(row.Rect.Y - s.RowsTop() + 1f));
    }

    [Fact]
    public void ExpandedRowsGoOnlyWhenTheRowIsGone()
    {
        var s = new ListFixture();
        s.Frames(2);
        s.State.Expansion.SetExpanded(new RowIdentity("b.zip"), true);
        s.Frames(3);

        s.Source.Replace([new ListFixture.Entry("a.zip"), new ListFixture.Entry("c.zip")]);
        s.Frames(3);

        Assert.Null(s.Harness.Gui.RootNode!.FindChildById("table/rowb.zip/details"));
        Assert.True(s.State.Expansion.IsExpanded(new RowIdentity("b.zip")));

        s.Source.Replace([new ListFixture.Entry("a.zip"), new ListFixture.Entry("b.zip")]);
        s.Frames(3);
        Assert.NotNull(s.Harness.Gui.RootNode.FindChildById("table/rowb.zip/details"));
    }

    [Fact]
    public void ASourceWithoutALookupCollapsesExpandedRowsWhenItsDataChanges()
    {
        var source = new PublishingSource { Pending = 20 };
        var details = new FakeDetails();
        var state = new TableState();
        var columns = Fixed(2, 100f);
        var options = new TableOptions<int> { RowDetails = details };
        using var harness = new FrameHarness(Width, Height);
        void Draw(Gui gui) => gui.Table(state, source, columns, options);
        harness.Frame(Draw);
        harness.Frame(Draw);

        state.Expansion.SetExpanded(RowIdentity.UnstableIndex(4), true, 4);
        harness.Frame(Draw);
        harness.Frame(Draw);
        Assert.NotNull(harness.Gui.RootNode!.FindChildById("table/row4/details"));

        source.Pending = 25;
        source.RaiseInvalidated();
        harness.Frame(Draw);
        harness.Frame(Draw);

        Assert.False(state.Expansion.IsExpanded(RowIdentity.UnstableIndex(4)));
        Assert.Null(harness.Gui.RootNode.FindChildById("table/row4/details"));
    }

    [Fact]
    public void TheBuiltInSourcesFindARowByIdentity()
    {
        var list = new ListTableSource<string>(["x", "y", "z"], static item => item);
        Assert.Equal(1, list.IndexOf(new RowIdentity("y")));
        Assert.Equal(-1, list.IndexOf(new RowIdentity("nope")));

        var stream = new StreamingTableSource<int>();
        stream.AppendRange([1, 2, 3]);
        stream.BeginFrame(1);
        Assert.Equal(2, stream.IndexOf(new RowIdentity("2")));
        Assert.Equal(-1, stream.IndexOf(new RowIdentity("3")));
        Assert.Equal(-1, stream.IndexOf(new RowIdentity("-1")));
        Assert.Equal(-1, stream.IndexOf(new RowIdentity("x")));
    }

    [Fact]
    public void AListSourceForgetsItsPositionsWhenItsOrderChanges()
    {
        var list = new ListTableSource<Item>([new Item("1", "b", 2), new Item("2", "a", 1)],
            static item => item.FileName);
        var column = TableColumn<Item>.Of("Name", static item => item.FileName);
        Assert.Equal(0, list.IndexOf(new RowIdentity("b")));

        list.ApplySort([new TableSort(column)]);
        list.BeginFrame(1);

        Assert.Equal(1, list.IndexOf(new RowIdentity("b")));
    }

    sealed class FakeDetails : ITableRowDetails<int>
    {
        public List<TableRowContext<int>> Rendered { get; } = [];

        public bool HasDetails(in TableRowContext<int> row) => row.Index % 2 == 0;

        public float GetHeight(in TableRowContext<int> row) => Detail;

        public void Render(Gui gui, in TableRowContext<int> row, float width, float height)
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                Rendered.Add(row);
            }
        }
    }

    sealed class Fixture
    {
        readonly GridSource _source;
        readonly TableColumns<int> _columns;
        readonly TableOptions<int> _options;

        public Fixture(int rows = 40, bool expander = false, bool selection = false)
        {
            _source = new GridSource(rows);
            _columns = expander
                ? new TableColumns<int>([TableColumn<int>.Expander(), .. Fixed(2, 100f)])
                : Fixed(3, 100f);
            _options = new TableOptions<int>
            {
                RowDetails = Details,
                Selection = selection ? TableSelectionMode.Multiple : TableSelectionMode.None
            };
            Harness = new FrameHarness(Width, Height);
            State = new TableState();
            Draw = gui => gui.Table(State, _source, _columns, _options);
        }

        public FakeDetails Details { get; } = new();

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

        public float RowY(int row) => Harness.Gui.RootNode!.FindChildById($"table/row{row}")!.Rect.Y;

        public Vector2 RowPoint(int row)
        {
            var node = Harness.Gui.RootNode!.FindChildById($"table/row{row}")!;
            return new Vector2(node.Rect.X + 20f, node.Rect.Y + (node.Rect.H / 2f));
        }

        public void Key(KeyboardKey key)
        {
            Harness.Input.PressKey(key);
            Frames(1);
            Harness.Input.ReleaseKey(key);
            Frames(2);
        }
    }

    sealed class ListFixture
    {
        public sealed record Entry(string Name);

        public ListFixture()
        {
            Columns = new TableColumns<Entry>(
            [
                TableColumn<Entry>.Of("Name", static entry => entry.Name, id: "name", width: 200f)
            ]);
            Source = new ListTableSource<Entry>([new Entry("a.zip"), new Entry("b.zip"), new Entry("c.zip")],
                static entry => entry.Name);
            Options = new TableOptions<Entry> { RowDetails = new AlwaysDetails() };
            Harness = new FrameHarness(Width, Height);
            State = new TableState();
            Draw = gui => gui.Table(State, Source, Columns, Options);
        }

        public TableColumns<Entry> Columns { get; }

        public ListTableSource<Entry> Source { get; }

        public TableOptions<Entry> Options { get; }

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

        public float RowsTop() => Harness.Gui.RootNode!.FindChildById("table/body")!.Rect.Y;

        sealed class AlwaysDetails : ITableRowDetails<Entry>
        {
            public bool HasDetails(in TableRowContext<Entry> row) => true;

            public float GetHeight(in TableRowContext<Entry> row) => Detail;

            public void Render(Gui gui, in TableRowContext<Entry> row, float width, float height)
            {
            }
        }
    }
}

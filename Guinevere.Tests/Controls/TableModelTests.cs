using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Covers the table's data model: column definitions, row identities and the per-table cache.</summary>
public class TableModelTests
{
    [Fact]
    public void ARowIdentityFromAnIndexIsItsInvariantNumber()
    {
        Assert.Equal("42", RowIdentity.FromIndex(42).Key);
        Assert.Equal("42", RowIdentity.FromIndex(42).ToString());
        Assert.Equal(RowIdentity.FromIndex(7), new RowIdentity("7"));
    }

    [Fact]
    public void AColumnNeedsAnIdAndATitle()
    {
        Assert.Throws<ArgumentException>(static () => new TableColumn("", "Title"));
        Assert.Throws<ArgumentNullException>(static () => new TableColumn("id", null!));
        Assert.Equal("", new TableColumn("id", "").Title);
    }

    [Fact]
    public void AColumnStartsVisibleUnpinnedAndSharingTheFreeWidth()
    {
        var column = new TableColumn("id", "Title");

        Assert.True(column.InitiallyVisible);
        Assert.Equal(TablePin.None, column.Pin);
        Assert.Equal(TableAlignment.Left, column.Alignment);
        Assert.Equal(TableLengthKind.Star, column.Width.Kind);
        Assert.Null(column.Renderer);
    }

    [Fact]
    public void AnAutoWidthIsMeasuredOncePerFontSize()
    {
        var state = new TableState();
        var column = new TableColumn("id", "Title");
        var measured = 0;

        float Measure()
        {
            measured++;
            return 50f + measured;
        }

        Assert.Equal(51f, state.AutoWidth(column, 12f, Measure));
        Assert.Equal(51f, state.AutoWidth(column, 12f, Measure));
        Assert.Equal(1, measured);

        Assert.Equal(52f, state.AutoWidth(column, 14f, Measure));
        Assert.Equal(2, measured);
    }

    [Fact]
    public void AnAutoColumnIsAsWideAsItsHeaderText()
    {
        TableColumn[] columns =
        [
            new("short", "A") { Width = TableLength.Auto, MinWidth = 1f },
            new("long", "A considerably longer header title") { Width = TableLength.Auto, MinWidth = 1f }
        ];

        var gui = RunFrame(columns);

        var shortWidth = gui.RootNode!.FindChildById("table/header/short")!.Rect.W;
        var longWidth = gui.RootNode!.FindChildById("table/header/long")!.Rect.W;
        Assert.True(shortWidth >= 2 * TableTheme.Default.CellPadding, "padding is part of the width");
        Assert.True(longWidth > shortWidth + 50f, $"{longWidth} should be well over {shortWidth}");
    }

    static Gui RunFrame(TableColumn[] columns)
    {
        using var surface = SKSurface.Create(new SKImageInfo(600, 200));
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(600, 200);
        var state = new TableState();
        var source = new EmptySource();

        for (var frame = 0; frame < 2; frame++)
        {
            gui.Time.Update(0.016);
            gui.SetStage(Pass.Pass1Build);
            gui.BeginFrame(surface.Canvas);
            gui.Table(state, source, columns);
            gui.CalculateLayout();
            gui.SetStage(Pass.Pass2Render);
            gui.Table(state, source, columns);
            gui.Render();
            gui.EndFrame();
        }

        return gui;
    }

    sealed class EmptySource : ITableViewSource
    {
        public int RowCount => 0;

        public event EventHandler? Invalidated
        {
            add { }
            remove { }
        }

        public object? GetValue(int row, TableColumn column) => null;

        public RowIdentity GetRowIdentity(int row) => RowIdentity.FromIndex(row);
    }
}

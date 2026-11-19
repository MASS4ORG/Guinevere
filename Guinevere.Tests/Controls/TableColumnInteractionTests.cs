using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the table header as the user meets it: the arrangement the chosen columns produce, the grab zone on a
/// header's edge, resizing and moving by pointer and by key, and the menu that shows and hides columns.
/// </summary>
public class TableColumnInteractionTests : TableTestBase
{
    [Fact]
    public void TheChosenOrderVisibilityAndWidthShapeTheHeaderAndTheCells()
    {
        var columns = Fixed(3, 100f);
        var state = new TableState();
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(Draw(state, columns));
        state.Columns.MoveTo(columns[0], columns[2]);
        state.Columns.SetVisible(columns[1], false);
        state.Columns.SetWidth(columns[0], TableLength.Pixels(150f));

        harness.Frame(Draw(state, columns));

        var gui = harness.Gui;
        Assert.Null(gui.RootNode!.FindChildById("table/header/c1"));
        Assert.Equal(["c2", "c0"], CellIds(gui, 0));
        Assert.Equal(100f, gui.RootNode!.FindChildById("table/header/c2")!.Rect.W);
        Assert.Equal(150f, gui.RootNode!.FindChildById("table/header/c0")!.Rect.W);
        Assert.Equal(100f, gui.RootNode!.FindChildById("table/header/c0")!.Rect.X, 0.5f);
    }

    [Fact]
    public void EveryResizableHeaderHasAGrabZoneOnItsRightEdge()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row, id: "a", width: 100f),
            TableColumn<int>.Of("B", static row => row, id: "b", width: 100f, resizable: false)
        ]);
        using var harness = new FrameHarness(Width, Height);

        harness.Frame(Draw(new TableState(), columns));

        var cell = harness.Gui.RootNode!.FindChildById("table/header/a")!;
        var handle = harness.Gui.RootNode.FindChildById("table/header/a/resize")!;
        Assert.InRange(handle.Rect.X, cell.Rect.X + cell.Rect.W - 7f, cell.Rect.X + cell.Rect.W);
        Assert.Equal(cell.Rect.H, handle.Rect.H);
        Assert.Null(harness.Gui.RootNode.FindChildById("table/header/b/resize"));
    }

    [Fact]
    public void TheTableOptionsTurnTheGrabZoneOff()
    {
        using var harness = new FrameHarness(Width, Height);

        harness.Frame(Draw(new TableState(), Fixed(2, 100f), new TableOptions { Resizable = false }));

        Assert.Null(harness.Gui.RootNode!.FindChildById("table/header/c0/resize"));
    }

    [Fact]
    public void DraggingTheEdgeSetsTheWidthFromWhereTheDragStarted()
    {
        var columns = Fixed(3, 100f);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        harness.Frame(draw);
        var edge = EdgeOf(harness, "c0");

        harness.Input.MoveTo(edge);
        harness.Input.PressButton();
        harness.Frame(draw);
        harness.Input.MoveTo(edge + new Vector2(40f, 0f));
        harness.Frame(draw);
        harness.Frame(draw);

        Assert.Equal(TableLength.Pixels(140f), state.Columns.WidthOf(columns[0]));
        Assert.Equal(140f, harness.Gui.RootNode!.FindChildById("table/header/c0")!.Rect.W, 0.5f);

        harness.Input.MoveTo(edge + new Vector2(-30f, 0f));
        harness.Frame(draw);
        Assert.Equal(TableLength.Pixels(70f), state.Columns.WidthOf(columns[0]));

        harness.Input.ReleaseButton();
        harness.Frame(draw);
        harness.Input.MoveTo(edge + new Vector2(200f, 0f));
        harness.Frame(draw);
        Assert.Equal(TableLength.Pixels(70f), state.Columns.WidthOf(columns[0]));
    }

    [Fact]
    public void ADragNeverMovesAColumnThatOptedOut()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row, id: "a", width: 100f, resizable: false),
            TableColumn<int>.Of("B", static row => row, id: "b", width: 100f)
        ]);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        var cell = harness.Gui.RootNode!.FindChildById("table/header/a")!;
        var edge = new Vector2(cell.Rect.X + cell.Rect.W - 3f, cell.Rect.Y + 5f);

        harness.Input.MoveTo(edge);
        harness.Input.PressButton();
        harness.Frame(draw);
        harness.Input.MoveTo(edge + new Vector2(50f, 0f));
        harness.Frame(draw);

        Assert.Null(state.Columns.WidthOf(columns[0]));
    }

    [Fact]
    public void ShiftWithAnArrowResizesTheFocusedHeaderByAStep()
    {
        var columns = Fixed(2, 100f);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        harness.Click(draw, FrameHarness.Center(harness.Gui.RootNode!.FindChildById("table/header/c0")!));

        PressChord(harness, draw, KeyboardKey.LeftShift, KeyboardKey.Right);

        Assert.Equal(TableLength.Pixels(116f), state.Columns.WidthOf(columns[0]));
        Assert.Null(state.Columns.WidthOf(columns[1]));

        PressChord(harness, draw, KeyboardKey.LeftShift, KeyboardKey.Left);
        Assert.Equal(TableLength.Pixels(100f), state.Columns.WidthOf(columns[0]));
    }

    [Fact]
    public void ControlWithAnArrowMovesTheFocusedHeaderOnePlace()
    {
        var columns = Fixed(3, 100f);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        harness.Click(draw, FrameHarness.Center(harness.Gui.RootNode!.FindChildById("table/header/c0")!));

        PressChord(harness, draw, KeyboardKey.LeftControl, KeyboardKey.Right);

        Assert.Equal(["c1", "c0", "c2"], HeaderOrder(harness.Gui, 3));
    }

    [Fact]
    public void AnArrowWithoutAModifierLeavesTheColumnsAlone()
    {
        var columns = Fixed(2, 100f);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        harness.Click(draw, FrameHarness.Center(harness.Gui.RootNode!.FindChildById("table/header/c0")!));

        harness.Input.PressKey(KeyboardKey.Right);
        harness.Frame(draw);
        harness.Input.ReleaseKey(KeyboardKey.Right);
        harness.Frame(draw);

        Assert.Equal(0, state.Columns.Save().Count(static setting => setting.Width is not null));
        Assert.Equal(["c0", "c1"], HeaderOrder(harness.Gui, 2));
    }

    [Fact]
    public void TheKeysDoNothingWhenTheTableTurnedThemOff()
    {
        var columns = Fixed(2, 100f);
        var state = new TableState();
        var options = new TableOptions { Resizable = false, Reorderable = false };
        var draw = Draw(state, columns, options);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        harness.Click(draw, FrameHarness.Center(harness.Gui.RootNode!.FindChildById("table/header/c0")!));

        PressChord(harness, draw, KeyboardKey.LeftShift, KeyboardKey.Right);
        PressChord(harness, draw, KeyboardKey.LeftControl, KeyboardKey.Right);

        Assert.Null(state.Columns.WidthOf(columns[0]));
        Assert.Equal(["c0", "c1"], HeaderOrder(harness.Gui, 2));
    }

    [Fact]
    public void DraggingAHeaderOntoAnotherMovesTheColumn()
    {
        var columns = Fixed(3, 100f);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        harness.Frame(draw);
        var from = FrameHarness.Center(harness.Gui.RootNode!.FindChildById("table/header/c0")!);
        var to = FrameHarness.Center(harness.Gui.RootNode.FindChildById("table/header/c2")!);

        harness.Input.MoveTo(from);
        harness.Input.PressButton();
        harness.Frame(draw);
        harness.Input.MoveTo(from + new Vector2(30f, 0f));
        harness.Frame(draw);
        harness.Frame(draw);
        harness.Input.MoveTo(to);
        harness.Frame(draw);
        harness.Input.ReleaseButton();
        harness.Frame(draw);
        harness.Frame(draw);

        Assert.Equal(["c1", "c2", "c0"], HeaderOrder(harness.Gui, 3));
    }

    [Fact]
    public void ARightClickOnTheHeaderOpensTheColumnMenu()
    {
        var columns = Fixed(2, 100f);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);

        harness.Click(draw, FrameHarness.Center(harness.Gui.RootNode!.FindChildById("table/header/c0")!),
            MouseButton.Right);

        Assert.True(harness.Gui.GetValue(false, "table/header/menu"));
    }

    [Fact]
    public void TheColumnMenuIsAbsentWhenTheTableTurnedItOff()
    {
        var state = new TableState();
        var draw = Draw(state, Fixed(2, 100f), new TableOptions { ColumnMenu = false });
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);

        harness.Click(draw, FrameHarness.Center(harness.Gui.RootNode!.FindChildById("table/header/c0")!),
            MouseButton.Right);

        Assert.False(harness.Gui.GetValue(false, "table/header/menu"));
    }

    [Fact]
    public void TheArrangementIsMadeAgainOnlyWhenTheChoicesChange()
    {
        var columns = Fixed(4, 100f);
        var state = new TableState();
        var draw = Draw(state, columns);
        using var harness = new FrameHarness(Width, Height);
        harness.Frame(draw);
        state.Columns.MoveTo(columns[0], columns[3]);
        harness.Frame(draw);
        var view = state.Cache.View;

        harness.Frame(draw);
        harness.Frame(draw);

        Assert.Same(view, state.Cache.View);
        Assert.Equal(["c1", "c2", "c3", "c0"], HeaderOrder(harness.Gui, 4));
    }

    Action<Gui> Draw(TableState state, TableColumns<int> columns, TableOptions? options = null)
    {
        var source = new GridSource(30);
        return gui => gui.Table(state, source, columns, options, Theme);
    }

    static Vector2 EdgeOf(FrameHarness harness, string column)
    {
        var cell = harness.Gui.RootNode!.FindChildById($"table/header/{column}")!;
        return new Vector2(cell.Rect.X + cell.Rect.W - 3f, cell.Rect.Y + 5f);
    }

    /// <summary>Holds a modifier, taps a key and lets both go, one frame each.</summary>
    static void PressChord(FrameHarness harness, Action<Gui> draw, KeyboardKey modifier, KeyboardKey key)
    {
        harness.Input.PressKey(modifier);
        harness.Input.PressKey(key);
        harness.Frame(draw);
        harness.Input.ReleaseKey(key);
        harness.Input.ReleaseKey(modifier);
        harness.Frame(draw);
    }

    static List<string> HeaderOrder(Gui gui, int count) =>
    [
        .. Enumerable.Range(0, count)
            .Select(i => (Id: $"c{i}", X: gui.RootNode!.FindChildById($"table/header/c{i}")!.Rect.X))
            .OrderBy(static column => column.X)
            .Select(static column => column.Id)
    ];
}

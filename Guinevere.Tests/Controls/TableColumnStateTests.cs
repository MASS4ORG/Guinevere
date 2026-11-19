namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers what the user chooses about a table's columns, kept by id: visibility, order, width, how the choices
/// arrange a column set, how a resize turns a wanted width into a column length, and how they are saved.
/// </summary>
public class TableColumnStateTests
{
    [Fact]
    public void AColumnIsVisibleAsDefinedUntilTheUserChoosesOtherwise()
    {
        var columns = Columns("a", "b");
        var hidden = TableColumn<int>.Of("H", static row => row, id: "h", visible: false);
        var state = new TableColumnState();

        Assert.True(state.IsVisible(columns[0]));
        Assert.False(state.IsVisible(hidden));

        Assert.True(state.SetVisible(hidden, true));
        Assert.True(state.IsVisible(hidden));
        Assert.True(state.SetVisible(columns[0], false));
        Assert.False(state.IsVisible(columns[0]));
        Assert.False(state.SetVisible(columns[0], false));
    }

    [Fact]
    public void TheLastShownColumnCannotBeHidden()
    {
        var columns = Columns("a", "b");
        var state = new TableColumnState();
        state.Bind(columns.Set);

        Assert.True(state.SetVisible(columns[0], false));
        var version = state.Version;
        Assert.False(state.SetVisible(columns[1], false));

        Assert.True(state.IsVisible(columns[1]));
        Assert.Equal(version, state.Version);
    }

    [Fact]
    public void AColumnTakesThePlaceOfTheOneItIsDroppedOn()
    {
        var columns = Columns("a", "b", "c", "d");
        var state = new TableColumnState();
        state.Bind(columns.Set);

        Assert.True(state.MoveTo(columns[0], columns[2]));
        Assert.Equal(["b", "c", "a", "d"], Ids(columns, state));

        Assert.True(state.MoveTo(columns[3], columns[1]));
        Assert.Equal(["d", "b", "c", "a"], Ids(columns, state));
    }

    [Fact]
    public void AMoveThatChangesNothingSaysSo()
    {
        var columns = Columns("a", "b");
        var state = new TableColumnState();
        state.Bind(columns.Set);
        var version = state.Version;

        Assert.False(state.MoveTo(columns[0], columns[0]));
        Assert.Equal(version, state.Version);
        Assert.Throws<ArgumentNullException>(() => state.MoveTo(columns[0], null!));
    }

    [Fact]
    public void ColumnsOnlyMoveWithinTheEdgeTheyArePinnedTo()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row, id: "a", pin: TablePin.Left),
            TableColumn<int>.Of("B", static row => row, id: "b", pin: TablePin.Left),
            TableColumn<int>.Of("C", static row => row, id: "c"),
            TableColumn<int>.Of("D", static row => row, id: "d", pin: TablePin.Right)
        ]);
        var state = new TableColumnState();
        state.Bind(columns.Set);

        Assert.False(state.MoveTo(columns[0], columns[2]));
        Assert.False(state.MoveTo(columns[2], columns[3]));
        Assert.True(state.MoveTo(columns[0], columns[1]));
        Assert.Equal(["b", "a", "c", "d"], Ids(columns, state));
    }

    [Fact]
    public void MovingBeforeTheFirstFrameSaysTheColumnsAreNotKnownYet()
    {
        var columns = Columns("a", "b");

        Assert.Throws<InvalidOperationException>(() => new TableColumnState().MoveTo(columns[0], columns[1]));
    }

    [Fact]
    public void AHiddenColumnKeepsItsPlaceWhileOthersMove()
    {
        var columns = Columns("a", "b", "c", "d");
        var state = new TableColumnState();
        state.Bind(columns.Set);
        state.SetVisible(columns[1], false);

        state.MoveTo(columns[0], columns[2]);

        Assert.Equal(["c", "a", "d"], Ids(columns, state));
        state.SetVisible(columns[1], true);
        Assert.Equal(["b", "c", "a", "d"], Ids(columns, state));
    }

    [Fact]
    public void ANudgeMovesPastTheNextShownColumnOfTheSameEdge()
    {
        var columns = Columns("a", "b", "c", "d");
        var state = new TableColumnState();
        state.Bind(columns.Set);
        state.SetVisible(columns[1], false);

        Assert.True(state.Nudge(columns[0], 1));
        Assert.Equal(["c", "a", "d"], Ids(columns, state));
        Assert.True(state.Nudge(columns[0], -1));
        Assert.Equal(["a", "c", "d"], Ids(columns, state));
        Assert.False(state.Nudge(columns[0], -1));
        Assert.False(state.Nudge(columns[3], 1));
        Assert.False(state.Nudge(columns[0], 0));
        Assert.False(new TableColumnState().Nudge(columns[0], 1));
    }

    [Fact]
    public void AWidthIsSetAndGivenBack()
    {
        var columns = Columns("a");
        var state = new TableColumnState();

        Assert.True(state.SetWidth(columns[0], TableLength.Pixels(80f)));
        Assert.False(state.SetWidth(columns[0], TableLength.Pixels(80f)));
        Assert.Equal(TableLength.Pixels(80f), state.WidthOf(columns[0]));

        Assert.True(state.ResetWidth(columns[0]));
        Assert.Null(state.WidthOf(columns[0]));
        Assert.False(state.ResetWidth(columns[0]));
    }

    [Fact]
    public void ResetForgetsEveryChoice()
    {
        var columns = Columns("a", "b");
        var state = new TableColumnState();
        state.Bind(columns.Set);
        state.MoveTo(columns[0], columns[1]);
        state.SetWidth(columns[0], TableLength.Pixels(10f));
        var version = state.Version;

        state.Reset();

        Assert.True(state.Version > version);
        Assert.Equal(["a", "b"], Ids(columns, state));
        Assert.Null(state.WidthOf(columns[0]));
    }

    [Fact]
    public void ArrangingAppliesOrderVisibilityWidthAndPinGroups()
    {
        var columns = new TableColumns<int>(
        [
            TableColumn<int>.Of("A", static row => row, id: "a", width: 100f),
            TableColumn<int>.Of("B", static row => row, id: "b", pin: TablePin.Left),
            TableColumn<int>.Of("C", static row => row, id: "c"),
            TableColumn<int>.Of("D", static row => row, id: "d", pin: TablePin.Right)
        ]);
        var state = new TableColumnState();
        state.Bind(columns.Set);
        state.MoveTo(columns[0], columns[2]);
        state.SetVisible(columns[3], false);
        state.SetWidth(columns[0], TableLength.Star(3f));

        var view = columns.Set.Arrange(state);

        Assert.Equal(["b", "c", "a"], view.Shown.Select(static column => column.Id));
        Assert.Equal(["table/header/b", "table/header/c", "table/header/a"], view.HeaderIds);
        Assert.Equal(["/b", "/c", "/a"], view.CellSuffixes);
        Assert.Equal(TableLength.Star(3f), view.Lengths[2]);
        Assert.Equal(columns[1].Width, view.Lengths[0]);
    }

    [Fact]
    public void ChoicesFollowTheirColumnsToARebuiltColumnList()
    {
        var first = Columns("a", "b", "c");
        var state = new TableColumnState();
        state.Bind(first.Set);
        state.MoveTo(first[0], first[2]);
        state.SetVisible(first[1], false);
        state.SetWidth(first[2], TableLength.Pixels(55f));

        var rebuilt = Columns("a", "b", "c", "d");
        var view = rebuilt.Set.Arrange(state);

        Assert.Equal(["c", "a", "d"], view.Shown.Select(static column => column.Id));
        Assert.Equal(TableLength.Pixels(55f), view.Lengths[0]);
    }

    [Fact]
    public void SavedChoicesRestoreInAnotherState()
    {
        var columns = Columns("a", "b", "c");
        var state = new TableColumnState();
        state.Bind(columns.Set);
        state.MoveTo(columns[2], columns[0]);
        state.SetVisible(columns[1], false);
        state.SetWidth(columns[0], TableLength.Star(2f));

        var saved = state.Save();
        var restored = new TableColumnState();
        restored.Restore(saved);

        Assert.Equal(
        [
            new TableColumnSetting("c", true, null),
            new TableColumnSetting("a", true, TableLength.Star(2f)),
            new TableColumnSetting("b", false, null)
        ], saved);
        Assert.Equal(["c", "a"], columns.Set.Arrange(restored).Shown.Select(static column => column.Id));
    }

    [Fact]
    public void RestoreKeepsUnknownIdsForLaterAndDropsRepeats()
    {
        var columns = Columns("a", "b");
        var state = new TableColumnState();
        state.Restore(
        [
            new TableColumnSetting("gone", false, TableLength.Pixels(9f)),
            new TableColumnSetting("b", true, null),
            new TableColumnSetting("b", false, null)
        ]);
        state.Bind(columns.Set);

        Assert.Equal(["b", "a"], Ids(columns, state));
        Assert.Equal(
        [
            new TableColumnSetting("b", true, null),
            new TableColumnSetting("a", true, null),
            new TableColumnSetting("gone", false, TableLength.Pixels(9f))
        ], state.Save());
        Assert.Throws<ArgumentNullException>(() => state.Restore(null!));
        Assert.Throws<ArgumentNullException>(() => state.Restore([new TableColumnSetting(null!, true, null)]));
    }

    [Fact]
    public void SavingBeforeTheFirstFrameSavesWhatWasRestored()
    {
        var state = new TableColumnState();
        var saved = new[] { new TableColumnSetting("x", false, null) };
        state.Restore(saved);

        Assert.Equal(saved, state.Save());
    }

    [Fact]
    public void AStarResizeSharesTheStarSpaceByWeight()
    {
        var (state, layout) = Resolved(400f, TableLength.Star(), TableLength.Star(), TableLength.Pixels(100f));

        Assert.True(state.Resize(layout, 0, 250f));

        var length = state.WidthOf(layout.Columns[0])!.Value;
        Assert.Equal(TableLengthKind.Star, length.Kind);
        Assert.Equal(250f, Resolve(400f, state, layout.Columns).Widths[0], 0.01f);
        Assert.Equal(50f, Resolve(400f, state, layout.Columns).Widths[1], 0.01f);
    }

    [Fact]
    public void ALoneStarColumnOrOneWantingMoreThanTheOthersCanGiveBecomesFixed()
    {
        var (state, lone) = Resolved(400f, TableLength.Star(), TableLength.Pixels(100f));
        state.Resize(lone, 0, 350f);
        Assert.Equal(TableLength.Pixels(350f), state.WidthOf(lone.Columns[0]));

        var (greedy, pair) = Resolved(400f, TableLength.Star(), TableLength.Star());
        greedy.Resize(pair, 0, 390f);
        Assert.Equal(TableLength.Pixels(390f), greedy.WidthOf(pair.Columns[0]));
    }

    [Fact]
    public void AFixedOrAutoColumnResizesToPixelsWithinItsLimits()
    {
        var narrow = TableColumn<int>.Of("N", static row => row, id: "n", width: 100f, minWidth: 40f, maxWidth: 120f);
        var state = new TableColumnState();
        var layout = TableLayout.Resolve([narrow], [narrow.Width], 400f, [0f]);

        state.Resize(layout, 0, 500f);
        Assert.Equal(TableLength.Pixels(120f), state.WidthOf(narrow));
        state.Resize(layout, 0, 5f);
        Assert.Equal(TableLength.Pixels(40f), state.WidthOf(narrow));

        var auto = TableColumn<int>.Of("Auto", static row => row, id: "auto", width: TableLength.Auto);
        var autoLayout = TableLayout.Resolve([auto], [auto.Width], 400f, [64f]);
        state.Resize(autoLayout, 0, 90f);
        Assert.Equal(TableLength.Pixels(90f), state.WidthOf(auto));
    }

    [Fact]
    public void AKeyboardStepWidensOrNarrowsByASetAmount()
    {
        var (state, layout) = Resolved(400f, TableLength.Pixels(100f), TableLength.Pixels(100f));

        state.ResizeByStep(layout, 0, 1);
        Assert.Equal(TableLength.Pixels(116f), state.WidthOf(layout.Columns[0]));
        state.ResizeByStep(layout, 1, -1);
        Assert.Equal(TableLength.Pixels(84f), state.WidthOf(layout.Columns[1]));
    }

    static TableColumns<int> Columns(params string[] ids) =>
        new(ids.Select(static id => TableColumn<int>.Of(id.ToUpperInvariant(), static row => row, id: id)));

    static string[] Ids(TableColumns<int> columns, TableColumnState state) =>
        [.. columns.Set.Arrange(state).Shown.Select(static column => column.Id)];

    static (TableColumnState State, TableLayout Layout) Resolved(float viewport, params TableLength[] lengths)
    {
        var columns = lengths.Select(static (length, i) =>
            TableColumn<int>.Of("C" + i, static row => row, id: "c" + i, width: length, minWidth: 20f)).ToArray();
        var state = new TableColumnState();
        return (state, TableLayout.Resolve(columns, lengths, viewport, new float[columns.Length]));
    }

    static TableLayout Resolve(float viewport, TableColumnState state, TableColumn[] columns) =>
        TableLayout.Resolve(columns, [.. columns.Select(column => state.WidthOf(column) ?? column.Width)], viewport,
            new float[columns.Length]);
}

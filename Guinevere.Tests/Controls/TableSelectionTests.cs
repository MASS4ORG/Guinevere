namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the selection model apart from any window: clicks with and without modifiers, ranges, the keyboard row,
/// the explicit API, and what a change of data does to the remembered anchor.
/// </summary>
public class TableSelectionTests : TableTestBase
{
    static RowIdentity Id(int row) => RowIdentity.UnstableIndex(row);

    static int[] Selected(TableSelection selection) =>
        [.. selection.ToArray().Select(static identity => int.Parse(identity.Key)).Order()];

    [Fact]
    public void APlainClickSelectsOnlyThatRow()
    {
        var source = new GridSource(10);
        var selection = new TableSelection();

        Assert.True(selection.Click(source, TableSelectionMode.Multiple, 2, "2", false, false));
        selection.Click(source, TableSelectionMode.Multiple, 5, "5", false, false);

        Assert.Equal([5], Selected(selection));
        Assert.Equal(Id(5), selection.Current);
        Assert.False(selection.Click(source, TableSelectionMode.Multiple, 5, "5", false, false));
    }

    [Fact]
    public void ControlTogglesARowAndKeepsTheOthers()
    {
        var source = new GridSource(10);
        var selection = new TableSelection();
        selection.Click(source, TableSelectionMode.Multiple, 1, "1", false, false);

        selection.Click(source, TableSelectionMode.Multiple, 4, "4", true, false);
        Assert.Equal([1, 4], Selected(selection));

        selection.Click(source, TableSelectionMode.Multiple, 1, "1", true, false);
        Assert.Equal([4], Selected(selection));
    }

    [Fact]
    public void ShiftSelectsTheRangeFromTheAnchorAndControlShiftAddsIt()
    {
        var source = new GridSource(20);
        var selection = new TableSelection();
        selection.Click(source, TableSelectionMode.Multiple, 3, "3", false, false);

        selection.Click(source, TableSelectionMode.Multiple, 6, "6", false, true);
        Assert.Equal([3, 4, 5, 6], Selected(selection));

        selection.Click(source, TableSelectionMode.Multiple, 1, "1", false, true);
        Assert.Equal([1, 2, 3], Selected(selection));

        selection.Click(source, TableSelectionMode.Multiple, 10, "10", true, false);
        selection.Click(source, TableSelectionMode.Multiple, 12, "12", true, true);
        Assert.Equal([1, 2, 3, 10, 11, 12], Selected(selection));
    }

    [Fact]
    public void ShiftWithNoAnchorJustSelectsTheRow()
    {
        var source = new GridSource(10);
        var selection = new TableSelection();

        selection.Click(source, TableSelectionMode.Multiple, 4, "4", false, true);

        Assert.Equal([4], Selected(selection));
    }

    [Fact]
    public void SingleModeIgnoresModifiers()
    {
        var source = new GridSource(10);
        var selection = new TableSelection();
        selection.Click(source, TableSelectionMode.Single, 2, "2", false, false);

        selection.Click(source, TableSelectionMode.Single, 6, "6", true, false);
        selection.Click(source, TableSelectionMode.Single, 8, "8", false, true);

        Assert.Equal([8], Selected(selection));
    }

    [Fact]
    public void ARangeIsClampedToTheRowsThereAre()
    {
        var source = new GridSource(5);
        var selection = new TableSelection();

        selection.SelectRange(source, -3, 40, false);

        Assert.Equal([0, 1, 2, 3, 4], Selected(selection));
        Assert.False(selection.SelectRange(source, 0, 4, true));
    }

    [Fact]
    public void SelectAllTakesEveryRow()
    {
        var source = new GridSource(7);
        var selection = new TableSelection();

        Assert.True(selection.SelectAll(source));

        Assert.Equal(7, selection.Count);
    }

    [Fact]
    public void AMoveSelectsExtendsOrOnlyMovesByModifier()
    {
        var source = new GridSource(20);
        var selection = new TableSelection();
        selection.MoveTo(source, TableSelectionMode.Multiple, 2, false, false);
        Assert.Equal([2], Selected(selection));

        selection.MoveTo(source, TableSelectionMode.Multiple, 5, false, true);
        Assert.Equal([2, 3, 4, 5], Selected(selection));

        selection.MoveTo(source, TableSelectionMode.Multiple, 9, true, false);
        Assert.Equal([2, 3, 4, 5], Selected(selection));
        Assert.Equal(Id(9), selection.Current);
    }

    [Fact]
    public void AShiftMoveFromNothingStartsAtTheKeyboardRow()
    {
        var source = new GridSource(20);
        var selection = new TableSelection();
        selection.SetCurrent(4, "4");

        selection.MoveTo(source, TableSelectionMode.Multiple, 7, false, true);

        Assert.Equal([4, 5, 6, 7], Selected(selection));
    }

    [Fact]
    public void AMoveDoesNotSelectWhenSelectionIsOff()
    {
        var source = new GridSource(5);
        var selection = new TableSelection();

        Assert.False(selection.MoveTo(source, TableSelectionMode.None, 3, false, false));

        Assert.Equal(0, selection.Count);
        Assert.Equal(Id(3), selection.Current);
    }

    [Fact]
    public void SpaceTogglesTheKeyboardRowInMultipleAndSelectsItInSingle()
    {
        var source = new GridSource(5);
        var selection = new TableSelection();
        Assert.False(selection.ToggleCurrent(TableSelectionMode.Multiple));

        selection.SetCurrent(2, "2");
        selection.ToggleCurrent(TableSelectionMode.Multiple);
        Assert.Equal([2], Selected(selection));
        selection.ToggleCurrent(TableSelectionMode.Multiple);
        Assert.Equal(0, selection.Count);

        selection.SetCurrent(3, "3");
        selection.ToggleCurrent(TableSelectionMode.Single);
        selection.ToggleCurrent(TableSelectionMode.Single);
        Assert.Equal([3], Selected(selection));
        Assert.False(selection.ToggleCurrent(TableSelectionMode.None));
        Assert.NotNull(source);
    }

    [Fact]
    public void TheExplicitApiSelectsAddsRemovesTogglesAndClears()
    {
        var selection = new TableSelection();

        Assert.True(selection.Select(Id(1)));
        Assert.False(selection.Select(Id(1)));
        Assert.True(selection.Add(Id(2)));
        Assert.False(selection.Add(Id(2)));
        Assert.True(selection.IsSelected(Id(2)));
        Assert.True(selection.Remove(Id(1)));
        Assert.False(selection.Remove(Id(1)));
        Assert.True(selection.Toggle(Id(3)));
        Assert.False(selection.Toggle(Id(3)));
        selection.Set([Id(7), Id(8)]);
        Assert.Equal([7, 8], Selected(selection));
        Assert.True(selection.Clear());
        Assert.False(selection.Clear());
        Assert.Throws<ArgumentNullException>(() => selection.Set(null!));
    }

    [Fact]
    public void EveryChangeMovesTheVersionAndNoChangeDoesNot()
    {
        var selection = new TableSelection();
        var version = selection.Version;

        selection.Add(Id(1));
        Assert.True(selection.Version > version);
        version = selection.Version;

        selection.Add(Id(1));
        selection.Remove(Id(9));
        selection.Select(Id(1));
        Assert.Equal(version, selection.Version);
    }

    [Fact]
    public void AnAnchorOrKeyboardRowThatMovedAwayIsForgotten()
    {
        var source = new GridSource(10);
        var selection = new TableSelection();
        selection.Click(source, TableSelectionMode.Multiple, 6, "6", false, false);

        selection.Reconcile(source);
        Assert.Equal(6, selection.AnchorIndex);

        selection.SetCurrent(6, "not-the-row-there");
        selection.Click(new GridSource(3), TableSelectionMode.Multiple, 2, "2", false, false);
        selection.Reconcile(new GridSource(2));

        Assert.Equal(-1, selection.AnchorIndex);
        Assert.Equal(-1, selection.CurrentIndex);
        Assert.Null(selection.Current);
    }

    [Fact]
    public void ClearingForgetsTheAnchor()
    {
        var source = new GridSource(10);
        var selection = new TableSelection();
        selection.Click(source, TableSelectionMode.Multiple, 6, "6", false, false);

        selection.Clear();

        Assert.Equal(-1, selection.AnchorIndex);
        Assert.Null(selection.AnchorKey);
    }
}

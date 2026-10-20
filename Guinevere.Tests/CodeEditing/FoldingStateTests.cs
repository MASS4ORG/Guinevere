namespace Guinevere.Tests.CodeEditing;

/// <summary>Checks folding normalization, edit mapping and visible-row consistency.</summary>
public class FoldingStateTests
{
    /// <summary>Provider refreshes retain valid folds, update their endpoints and remove missing headers.</summary>
    [Fact]
    public void RefreshedRangesKeepOnlyValidFoldHeaders()
    {
        var folds = new FoldingState();
        folds.SetRanges([new(2, 5), new(10, 15)]);
        folds.Toggle(2);
        folds.Toggle(10);

        folds.SetRanges([new(2, 4), new(-1, 2), new(2, 8), new(9, 9), new(12, 11)]);

        Assert.Equal([new FoldingRange(2, 8)], folds.Ranges);
        Assert.Equal(8, Assert.Single(folds.Folded).Value);
        Assert.False(folds.IsHidden(10));
        Assert.Equal(new FoldingRange(2, 8), folds.RangeAt(2));
        Assert.Null(folds.RangeAt(1));
        Assert.Null(folds.RangeAt(3));
        Assert.False(folds.Toggle(10));
        Assert.True(folds.Toggle(2));
        Assert.Empty(folds.Folded);
    }

    /// <summary>Deleting a folded header unfolds that region while later regions follow their text.</summary>
    [Fact]
    public void DeletedFoldHeadersCannotMoveOntoEarlierText()
    {
        var folds = new FoldingState();
        folds.SetRanges([new(11, 20), new(25, 30)]);
        folds.Toggle(11);
        folds.Toggle(25);

        folds.Shift(10, -3);

        Assert.Equal(new KeyValuePair<int, int>(22, 27), Assert.Single(folds.Folded));
        Assert.False(folds.IsHidden(9));
        Assert.True(folds.IsHidden(23));
    }

    /// <summary>Nested and overlapping folds hide each line once and map visible rows back to their source lines.</summary>
    [Fact]
    public void NestedFoldsRoundTripEveryVisibleRow()
    {
        var folds = new FoldingState();
        folds.SetRanges([new(1, 8), new(2, 4), new(6, 10), new(14, 18)]);
        foreach (var line in new[] { 1, 2, 6, 14 })
        {
            folds.Toggle(line);
        }

        int[] visible = [0, 1, 11, 12, 13, 14, 19, 20];
        Assert.Equal(visible.Length, folds.RowCount(21));
        Assert.Equal(2, folds.RowCount(5));
        Assert.Equal(0, folds.RowCount(0));
        for (var row = 0; row < visible.Length; row++)
        {
            Assert.Equal(visible[row], folds.LineOfRow(row));
            Assert.Equal(row, folds.RowOfLine(visible[row]));
            Assert.False(folds.IsHidden(visible[row]));
        }
        Assert.Equal(1, folds.RowOfLine(7));
        Assert.Equal(5, folds.RowOfLine(17));
        Assert.False(folds.Reveal(12));
        Assert.True(folds.Reveal(3));
        Assert.False(folds.IsHidden(3));
        Assert.True(folds.IsHidden(7));
        folds.Clear();
        Assert.Equal(21, folds.RowCount(21));
        Assert.Equal(17, folds.LineOfRow(17));
        Assert.False(folds.Reveal(17));
    }

    /// <summary>Insertion and deletion adjust enclosing folds and preserve earlier unaffected ranges.</summary>
    [Fact]
    public void FoldEndpointsFollowInsertedAndRemovedLines()
    {
        var folds = new FoldingState();
        folds.Shift(0, 1);
        folds.SetRanges([new(0, 2), new(5, 9), new(12, 15)]);
        foreach (var line in new[] { 0, 5, 12 })
        {
            folds.Toggle(line);
        }
        folds.Shift(6, 0);
        folds.Shift(6, 2);
        Assert.Equal([new(0, 2), new(5, 11), new KeyValuePair<int, int>(14, 17)], folds.Folded);
        folds.Shift(6, -2);
        Assert.Equal([new(0, 2), new(5, 9), new KeyValuePair<int, int>(12, 15)], folds.Folded);
        folds.Shift(0, -20);
        Assert.Empty(folds.Folded);
    }
}

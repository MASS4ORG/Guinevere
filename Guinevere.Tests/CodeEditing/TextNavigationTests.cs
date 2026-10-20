namespace Guinevere.Tests.CodeEditing;

/// <summary>Navigation over UTF-16 text, grapheme clusters, word runs and tab-expanded columns.</summary>
public class TextNavigationTests
{
    /// <summary>Forward movement crosses a complete grapheme or one normalized line break.</summary>
    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("ab", 0, 1)]
    [InlineData("ab", 2, 2)]
    [InlineData("a\nb", 1, 2)]
    [InlineData("a\nb", 2, 3)]
    [InlineData("éx", 0, 2)]
    [InlineData("👍x", 0, 2)]
    [InlineData("👨‍👩‍👧x", 0, 8)]
    public void NextBoundaryKeepsGraphemesTogether(string text, int offset, int expected) =>
        Assert.Equal(expected, TextNavigation.NextBoundary(new StringTextBuffer(text), offset));

    /// <summary>Backward movement returns the previous grapheme or crosses to the preceding line break.</summary>
    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("ab", 0, 0)]
    [InlineData("ab", 2, 1)]
    [InlineData("a\nb", 2, 1)]
    [InlineData("a\nb", 3, 2)]
    [InlineData("éx", 2, 0)]
    [InlineData("👍x", 2, 0)]
    [InlineData("👨‍👩‍👧x", 8, 0)]
    public void PreviousBoundaryKeepsGraphemesTogether(string text, int offset, int expected) =>
        Assert.Equal(expected, TextNavigation.PreviousBoundary(new StringTextBuffer(text), offset));

    /// <summary>Column snapping never returns an offset inside a surrogate pair.</summary>
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(2, 1, 1)]
    [InlineData(3, 1, 3)]
    [InlineData(4, 3, 4)]
    [InlineData(99, 3, 4)]
    public void ElementColumnsSnapAtOrBeforeTheRequestedPosition(int column, int previous, int snapped)
    {
        Assert.Equal(previous, TextNavigation.PreviousElement("a👍b", column));
        Assert.Equal(snapped, TextNavigation.SnapToElement("a👍b", column));
        Assert.Equal(0, TextNavigation.PreviousElement("", column));
        Assert.Equal(0, TextNavigation.SnapToElement("", column));
    }

    /// <summary>Word movement skips whitespace and keeps identifier and punctuation runs distinct.</summary>
    [Theory]
    [InlineData("foo.bar  x", 0, 0, 3)]
    [InlineData("foo.bar  x", 3, 0, 4)]
    [InlineData("foo.bar  x", 4, 3, 7)]
    [InlineData("foo.bar  x", 7, 4, 10)]
    [InlineData("foo.bar  x", 9, 4, 10)]
    [InlineData("foo.bar  x", 10, 9, 10)]
    [InlineData("   ", 0, 0, 3)]
    [InlineData("   ", 3, 0, 3)]
    [InlineData("a\nb", 1, 0, 2)]
    [InlineData("a\nb", 2, 1, 3)]
    [InlineData("", 0, 0, 0)]
    public void WordMovementHonoursRunKindsAndLineBoundaries(string text, int offset, int left, int right)
    {
        ITextBuffer buffer = new StringTextBuffer(text);

        Assert.Equal(left, TextNavigation.WordLeft(buffer, offset));
        Assert.Equal(right, TextNavigation.WordRight(buffer, offset));
    }

    /// <summary>Identifier selection includes letters, digits and underscores but excludes punctuation.</summary>
    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(3, 0, 5)]
    [InlineData(5, 0, 5)]
    [InlineData(6, 6, 9)]
    [InlineData(9, 6, 9)]
    [InlineData(-10, 0, 5)]
    [InlineData(99, 6, 9)]
    public void WordAtFindsTheTouchingIdentifier(int offset, int start, int end)
    {
        ITextBuffer buffer = new StringTextBuffer("foo_2.bar");

        Assert.Equal((start, end), TextNavigation.WordAt(buffer, offset));
        Assert.Equal((0, 0), TextNavigation.WordAt(new StringTextBuffer(""), 0));
    }

    /// <summary>Tabs advance to the next tab stop while ordinary characters advance one column.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]
    [InlineData(2, 5)]
    [InlineData(3, 6)]
    [InlineData(4, 8)]
    [InlineData(5, 9)]
    [InlineData(99, 9)]
    public void VisualColumnsExpandTabs(int column, int expected) =>
        Assert.Equal(expected, TextNavigation.VisualColumn("\tab\tc", column, 4));

    /// <summary>Visual positions round to the nearest side of a tab stop.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 3)]
    [InlineData(7, 4)]
    [InlineData(8, 4)]
    [InlineData(9, 5)]
    [InlineData(99, 5)]
    public void CodeUnitColumnsRoundTabPositions(int visual, int expected) =>
        Assert.Equal(expected, TextNavigation.ColumnAt("\tab\tc", visual, 4));

    /// <summary>Visual column lookup snaps positions inside a grapheme back to its start.</summary>
    [Theory]
    [InlineData("éx", 1, 0)]
    [InlineData("éx", 2, 2)]
    [InlineData("👍x", 1, 0)]
    [InlineData("👍x", 2, 2)]
    [InlineData("", 10, 0)]
    public void CodeUnitColumnsPreserveGraphemeBoundaries(string text, int visual, int expected) =>
        Assert.Equal(expected, TextNavigation.ColumnAt(text, visual, 4));

    /// <summary>Indentation includes leading spaces and tabs only.</summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("abc", "")]
    [InlineData("\t  abc", "\t  ")]
    [InlineData("   ", "   ")]
    public void IndentationStopsBeforeTheFirstOtherCharacter(string text, string expected) =>
        Assert.Equal(expected, TextNavigation.Indentation(text));
}

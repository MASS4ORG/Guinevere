namespace Guinevere.Tests.CodeEditing;

/// <summary>Completion filtering and selection independent of commands and drawing.</summary>
public class CompletionSessionTests
{
    /// <summary>Filtering preserves the selected item when possible and navigation wraps in both directions.</summary>
    [Fact]
    public void FilteringPreservesSelectionAndNavigationWraps()
    {
        var session = new CompletionSession(new CompletionList(2, [new("alpha"), new("alpine"), new("beta")]));
        Assert.Equal(2, session.ReplaceStart);
        Assert.Equal("alpha", session.SelectedItem!.Label);
        Assert.True(session.Filter("al"));
        session.MoveSelection(-1);
        Assert.Equal("alpine", session.SelectedItem!.Label);
        Assert.True(session.Filter("alpi"));
        Assert.Equal(0, session.Selected);
        Assert.True(session.Filter(""));
        Assert.Equal("alpine", session.SelectedItem!.Label);
        session.MoveSelection(2);
        Assert.Equal("alpha", session.SelectedItem!.Label);
        session.Selected = -10;
        Assert.Equal("alpha", session.SelectedItem!.Label);
        session.Selected = 99;
        Assert.Equal("beta", session.SelectedItem!.Label);
    }

    /// <summary>Exact words and unmatched prefixes produce no selected item; empty lists remain navigable.</summary>
    [Fact]
    public void EmptyResultsCanBeClearedAndRestored()
    {
        var session = new CompletionSession(new CompletionList(0, [new("alpha")]));
        Assert.False(session.Filter("alpha"));
        Assert.Null(session.SelectedItem);
        session.MoveSelection(1);
        Assert.True(session.Filter(""));
        Assert.Equal(0, session.Selected);
        Assert.False(session.Filter("zzz"));
        Assert.Null(session.SelectedItem);
    }
}

namespace Guinevere.Tests.CodeEditing;

/// <summary>Search caching, match options and replacement edits independent of editor commands.</summary>
public class FindStateTests
{
    /// <summary>Case and identifier boundaries affect matches, and invalid patterns report an error.</summary>
    [Fact]
    public void SearchOptionsAndInvalidPatternsRefreshCachedMatches()
    {
        var document = new TextDocument(new StringTextBuffer("Foo food foo"));
        var find = new FindState();
        Assert.Empty(find.Matches(document));
        find.Query = "foo";
        var matches = find.Matches(document);
        Assert.Equal(3, matches.Count);
        Assert.Same(matches, find.Matches(document));
        find.WholeWord = true;
        Assert.Equal(2, find.Matches(document).Count);
        find.MatchCase = true;
        Assert.Single(find.Matches(document));
        find.UseRegex = true;
        find.Query = "(";
        Assert.Empty(find.Matches(document));
        Assert.NotNull(find.Error);
        find.Query = "missing";
        Assert.Empty(find.Matches(document));
        Assert.Null(find.Error);
        Assert.Empty(find.ReplaceAllEdits(document));
    }

    /// <summary>Document edits invalidate search results and plain replacements preserve original offsets.</summary>
    [Fact]
    public void PlainReplacementEditsApplyTogetherAndInvalidateTheSearch()
    {
        var document = new TextDocument(new StringTextBuffer("one one"));
        var find = new FindState { Query = "one", Replacement = "two" };
        var matches = find.Matches(document);
        Assert.Equal("two", find.ReplacementFor(document, matches[0]));

        document.Apply(find.ReplaceAllEdits(document), [TextSelection.At(0)]);

        Assert.Equal("two two", document.Buffer.Text);
        Assert.Empty(find.Matches(document));
        Assert.NotSame(matches, find.Matches(document));
    }

    /// <summary>Regular-expression captures expand for individual and bulk replacement edits.</summary>
    [Fact]
    public void RegexReplacementExpandsGroups()
    {
        var document = new TextDocument(new StringTextBuffer("a1 b22"));
        var find = new FindState { Query = @"(\d+)", Replacement = "<$1>", UseRegex = true };
        var matches = find.Matches(document);
        Assert.Equal("<1>", find.ReplacementFor(document, matches[0]));
        Assert.Equal("<$1>", find.ReplacementFor(document, new FindMatch(0, 1)));
        Assert.Equal(2, matches[0].End);

        document.Apply(find.ReplaceAllEdits(document), [TextSelection.At(0)]);

        Assert.Equal("a<1> b<22>", document.Buffer.Text);
        find.Query = "(";
        Assert.Equal("<$1>", find.ReplacementFor(document, new FindMatch(0, 1)));
    }

    /// <summary>Zero-length matches are omitted and large result sets stop at the documented limit.</summary>
    [Fact]
    public void SearchOmitsEmptyMatchesAndCapsLargeResults()
    {
        var document = new TextDocument(new StringTextBuffer(new string('a', FindState.MaxMatches + 10)));
        var find = new FindState { Query = "a" };
        Assert.Equal(FindState.MaxMatches, find.Matches(document).Count);
        find.Query = "(?=a)";
        find.UseRegex = true;
        Assert.Empty(find.Matches(document));
    }
}

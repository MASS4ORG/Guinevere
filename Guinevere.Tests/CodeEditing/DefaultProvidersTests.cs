namespace Guinevere.Tests.CodeEditing;

/// <summary>Checks folding and word completion against incomplete source text.</summary>
public class DefaultProvidersTests
{
    /// <summary>Asynchronous providers scan immutable snapshots and return their computed folds and completions.</summary>
    [Fact]
    public async Task ProvidersReturnResultsFromImmutableSnapshots()
    {
        var cancellation = TestContext.Current.CancellationToken;
        var indented = new TextSnapshot(new StringTextBuffer("root\n    alpha\n    alphabet\nal"), 0);
        var bracketed = new TextSnapshot(new StringTextBuffer("{\n a\n}"), 0);
        var words = new TextSnapshot(new StringTextBuffer("alpha alphabet\nal"), 0);

        var indentFolds = await IndentationFoldingProvider.Instance.GetFoldingRangesAsync(indented, cancellation);
        var bracketFolds = await BracketFoldingProvider.Instance.GetFoldingRangesAsync(bracketed, cancellation);
        var completions = await WordCompletionProvider.Instance.GetCompletionsAsync(words, words.Buffer.Length,
            cancellation);

        Assert.Equal([new FoldingRange(0, 2)], indentFolds);
        Assert.Equal([new FoldingRange(0, 1)], bracketFolds);
        Assert.Equal(["alpha", "alphabet"], completions!.Items.Select(item => item.Label));
    }

    /// <summary>A mismatched closing bracket cannot terminate a different opening delimiter.</summary>
    [Theory]
    [InlineData("{\n value\n)\n more\n}")]
    [InlineData("[\n value\n}\n more\n]")]
    [InlineData("(\n value\n]\n more\n)")]
    public void MismatchedBracketsDoNotCloseFolds(string source)
    {
        Assert.Equal([new FoldingRange(0, 3)],
            BracketFoldingProvider.Find(source, TestContext.Current.CancellationToken));
    }

    /// <summary>Quoted delimiters, escaped quotes and unfinished strings cannot create folds.</summary>
    [Theory]
    [InlineData("\"\\\"{\"\n x\n}")]
    [InlineData("'{\n x\n}")]
    [InlineData("`{")]
    [InlineData("\"{\\")]
    [InlineData("/* {\n */\n}")]
    [InlineData("// {\n}")]
    [InlineData("/* {")]
    [InlineData("// {")]
    [InlineData("{ x }")]
    [InlineData("/")]
    public void NonCodeAndSingleLineBracketsDoNotFold(string source) =>
        Assert.Empty(BracketFoldingProvider.Find(source, TestContext.Current.CancellationToken));

    /// <summary>Nested indentation honors tab stops and excludes trailing blank lines.</summary>
    [Fact]
    public void IndentationFoldsNestedBlocksAndStopsAtTheLastContentLine()
    {
        var buffer = new StringTextBuffer("root\n\tchild\n\t\tvalue\n\tend\n\nnext\n\n");
        Assert.Equal([new FoldingRange(0, 3), new FoldingRange(1, 2)],
            IndentationFoldingProvider.Find(buffer, TestContext.Current.CancellationToken)
                .OrderBy(range => range.StartLine));
        Assert.Empty(IndentationFoldingProvider.Find(new StringTextBuffer(" \n\t\n"),
            TestContext.Current.CancellationToken));
    }

    /// <summary>Completion ranks matches case-insensitively while excluding the currently typed word.</summary>
    [Fact]
    public void CompletionIgnoresPunctuationAndUnrelatedOrExactWords()
    {
        var buffer = new StringTextBuffer("Alpine, alpha; alphabet alphabet! beta al\nal");
        var list = WordCompletionProvider.Complete(buffer, buffer.Length)!;
        Assert.Equal(["alphabet", "Alpine", "alpha"], list.Items.Select(item => item.Label));
        Assert.Null(WordCompletionProvider.Complete(new StringTextBuffer("only"), 4));
        Assert.Null(WordCompletionProvider.Complete(new StringTextBuffer("word "), 5));
    }

    /// <summary>Provider tasks honor cancellation before scanning a document.</summary>
    [Fact]
    public async Task ProvidersHonorCancelledRequests()
    {
        var cancellation = new CancellationToken(true);
        var snapshot = new TextSnapshot(new StringTextBuffer("{\n a\n}"), 0);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BracketFoldingProvider.Instance.GetFoldingRangesAsync(snapshot, cancellation));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            IndentationFoldingProvider.Instance.GetFoldingRangesAsync(snapshot, cancellation));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WordCompletionProvider.Instance.GetCompletionsAsync(snapshot, 3, cancellation));
        Assert.ThrowsAny<OperationCanceledException>(() => BracketFoldingProvider.Find("{", cancellation));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            IndentationFoldingProvider.Find(snapshot.Buffer, cancellation));
    }
}

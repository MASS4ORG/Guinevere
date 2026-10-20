using static Guinevere.Tests.CodeEditing.EditorTestText;

namespace Guinevere.Tests.CodeEditing;

public class LanguageProviderTests
{
    /// <summary>A provider whose answers the test releases, to check versions and debouncing.</summary>
    sealed class ManualDiagnostics : IDiagnosticsProvider
    {
        internal readonly List<(TextSnapshot Snapshot, TaskCompletionSource<IReadOnlyList<TextDiagnostic>> Answer)>
            Requests = [];

        public Task<IReadOnlyList<TextDiagnostic>> GetDiagnosticsAsync(TextSnapshot snapshot,
            CancellationToken cancellation)
        {
            var answer = new TaskCompletionSource<IReadOnlyList<TextDiagnostic>>();
            Requests.Add((snapshot, answer));
            return answer.Task;
        }
    }

    sealed class FixedHover(HoverInfo? info) : IHoverProvider
    {
        internal int Calls;

        public Task<HoverInfo?> GetHoverAsync(TextSnapshot snapshot, int offset, CancellationToken cancellation)
        {
            Calls++;
            return Task.FromResult(info);
        }
    }

    sealed class ThrowingFolding : IFoldingProvider
    {
        public Task<IReadOnlyList<FoldingRange>> GetFoldingRangesAsync(TextSnapshot snapshot,
            CancellationToken cancellation) => throw new InvalidOperationException("broken provider");
    }

    [Fact]
    public void IndentationFoldsBlocksAndSkipsBlankLines()
    {
        var ranges = IndentationFoldingProvider.Find(new StringTextBuffer("def a():\n    x\n\n    y\nz\n  w"),
            TestContext.Current.CancellationToken);

        Assert.Equal([new FoldingRange(0, 3), new FoldingRange(4, 5)], ranges.OrderBy(range => range.StartLine));
    }

    [Fact]
    public void BracketsFoldButNotInsideStringsOrComments()
    {
        const string code = "void f() {\n  var s = \"{\";\n  // {\n  /* {\n  */ g(\n    1);\n}\n";

        var ranges = BracketFoldingProvider.Find(code, TestContext.Current.CancellationToken);

        Assert.Equal([new FoldingRange(0, 5)], ranges);
    }

    [Fact]
    public void WordCompletionOffersOtherWordsWithThePrefixMostFrequentFirst()
    {
        var buffer = new StringTextBuffer("counter count country counter\ncou");

        var list = WordCompletionProvider.Complete(buffer, buffer.Length);

        Assert.NotNull(list);
        Assert.Equal(buffer.Length - 3, list!.ReplaceStart);
        Assert.Equal(["counter", "count", "country"], list.Items.Select(item => item.Label));
        Assert.Null(WordCompletionProvider.Complete(buffer, 0));
    }

    [Fact]
    public async Task DiagnosticsWaitForTypingToPauseAndStaleAnswersAreDropped()
    {
        var provider = new ManualDiagnostics();
        var state = Editor("ab|", LanguageServices.PlainText with { Diagnostics = provider });

        state.Update(0.0);
        Assert.Single(provider.Requests);
        state.Type("c");
        state.Update(0.1);
        Assert.Single(provider.Requests);
        provider.Requests[0].Answer.SetResult([new TextDiagnostic(0, 1, DiagnosticSeverity.Error, "old")]);
        await provider.Requests[0].Answer.Task;
        state.Update(0.2);
        Assert.Empty(state.Diagnostics);
        state.Update(0.5);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal("abc", provider.Requests[1].Snapshot.Buffer.Text);
        provider.Requests[1].Answer.SetResult([new TextDiagnostic(1, 2, DiagnosticSeverity.Warning, "new")]);
        await provider.Requests[1].Answer.Task;
        state.Update(0.6);
        Assert.Equal("new", state.Diagnostics.Single().Message);
        await WaitFor(() =>
        {
            state.Update(0.6);
            return !state.IsWaitingForProviders;
        });
    }

    [Fact]
    public async Task DiagnosticsMoveWithEditsUntilTheProviderAnswersAgain()
    {
        var provider = new ManualDiagnostics();
        var state = Editor("|abc", LanguageServices.PlainText with { Diagnostics = provider });
        state.Update(0);
        provider.Requests[0].Answer.SetResult([new TextDiagnostic(1, 2, DiagnosticSeverity.Error, "x")]);
        await provider.Requests[0].Answer.Task;
        state.Update(0);

        state.Type("12");

        Assert.Equal(new TextDiagnostic(3, 2, DiagnosticSeverity.Error, "x"), state.Diagnostics.Single());
    }

    [Fact]
    public async Task CompletionOpensFiltersAsYouTypeAndAccepts()
    {
        var state = Editor("counter country\nco|", settings: new TextEditorSettings());
        state.Language = state.Language with { Completion = WordCompletionProvider.Instance };

        state.Type("u");
        await WaitFor(() => { state.Update(0); return state.Completion is not null; });
        Assert.Equal(["counter", "country"], state.Completion!.Items.Select(item => item.Label).Order());
        state.Type("n");
        state.Update(0);
        state.Execute("nextSuggestion");
        state.Execute("previousSuggestion");
        Assert.Equal(2, state.Completion!.Items.Count);
        state.Type("tr");
        state.Update(0);
        Assert.Equal(["country"], state.Completion!.Items.Select(item => item.Label));
        state.Execute("acceptSuggestion");
        Assert.Equal("counter country\ncountry|", Show(state));
        Assert.Null(state.Completion);
    }

    [Fact]
    public async Task CompletionClosesWhenTheCaretLeavesTheWord()
    {
        var state = Editor("alpha\nal|");
        state.TriggerCompletion();
        await WaitFor(() => { state.Update(0); return state.Completion is not null; });

        state.Execute("cursorUp");
        state.Update(0);

        Assert.Null(state.Completion);
    }

    [Fact]
    public async Task HoverAsksOncePerRangeAndClears()
    {
        var provider = new FixedHover(new HoverInfo(0, 3, "**doc**"));
        var state = Editor("abc", LanguageServices.PlainText with { Hover = provider });

        state.RequestHover(1);
        await WaitFor(() => { state.Update(0); return state.Hover is not null; });
        state.RequestHover(2);
        Assert.Equal(1, provider.Calls);
        state.ClearHover();
        Assert.Null(state.Hover);
    }

    [Fact]
    public async Task ProviderFailuresAreIgnored()
    {
        var state = Editor("a", LanguageServices.PlainText with { Folding = new ThrowingFolding() });

        state.Update(1);
        await Task.Yield();
        state.Update(1);

        Assert.Empty(state.FoldingRanges);
    }

    [Fact]
    public void TokenStatesCatchUpWithinTheBudgetAndRestartAfterEdits()
    {
        var cache = new SyntaxTokenCache();
        cache.Use(CLikeSyntax.CSharp);
        var buffer = new StringTextBuffer("/* a\nb */ int x;\nint y;");
        var tokens = new List<SyntaxToken>();

        var budget = 1;
        Assert.False(cache.TryTokenize(buffer, 2, ref budget, tokens));
        budget = 5;
        Assert.True(cache.TryTokenize(buffer, 1, ref budget, tokens));
        Assert.Equal(SyntaxKind.Comment, tokens[0].Kind);
        Assert.Equal(2, cache.KnownLines);
        cache.Invalidate(0);
        Assert.Equal(1, cache.KnownLines);
    }

    [Fact]
    public void FoldsMapRowsToLinesAndFollowEdits()
    {
        var state = Editor("{\n a\n b\n}\n{\n c\n}");
        var folds = state.Folds;
        folds.SetRanges([new FoldingRange(0, 2), new FoldingRange(4, 5), new FoldingRange(9, 8)]);

        Assert.True(state.ToggleFold(0));
        Assert.False(state.ToggleFold(1));
        Assert.Equal(5, folds.RowCount(7));
        Assert.Equal(3, folds.LineOfRow(1));
        Assert.Equal(0, folds.RowOfLine(2));
        Assert.True(folds.IsHidden(1));
        state.Document.Apply([new TextEdit(0, 0, "x\n")], [TextSelection.At(0)]);
        Assert.True(state.IsFolded(0));
        Assert.True(folds.IsHidden(3));
        Assert.True(folds.Reveal(2));
        Assert.False(state.IsFolded(0));
    }

    [Fact]
    public void FoldCommandsFoldTheRegionAroundTheCaret()
    {
        var state = Editor("{\n a|\n}");
        state.Folds.SetRanges([new FoldingRange(0, 1)]);

        state.Execute("fold");
        Assert.True(state.IsFolded(0));
        Assert.Equal("{|\n a\n}", Show(state));
        state.Execute("unfold");
        Assert.False(state.IsFolded(0));
    }

    static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }
}

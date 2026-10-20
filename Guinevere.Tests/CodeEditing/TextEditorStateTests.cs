namespace Guinevere.Tests.CodeEditing;

/// <summary>Editor state updates and provider results independent of editing commands and drawing.</summary>
public class TextEditorStateTests
{
    /// <summary>Indentation settings choose the configured spaces or one literal tab.</summary>
    [Fact]
    public void IndentationUsesConfiguredSpacesOrTabs()
    {
        Assert.Equal("    ", new TextEditorSettings().IndentUnit);
        Assert.Equal("  ", new TextEditorSettings { TabSize = 2 }.IndentUnit);
        Assert.Equal("\t", new TextEditorSettings { InsertSpaces = false }.IndentUnit);
    }

    sealed class ManualDiagnostics : IDiagnosticsProvider
    {
        internal List<(TextSnapshot Snapshot, TaskCompletionSource<IReadOnlyList<TextDiagnostic>> Answer)> Requests
        { get; } = [];

        /// <inheritdoc />
        public Task<IReadOnlyList<TextDiagnostic>> GetDiagnosticsAsync(TextSnapshot snapshot,
            CancellationToken cancellation)
        {
            var answer = new TaskCompletionSource<IReadOnlyList<TextDiagnostic>>();
            Requests.Add((snapshot, answer));
            return answer.Task;
        }
    }

    sealed class FixedCompletion(CompletionList? list) : ICompletionProvider
    {
        /// <inheritdoc />
        public Task<CompletionList?> GetCompletionsAsync(TextSnapshot snapshot, int offset,
            CancellationToken cancellation) =>
            Task.FromResult(list);
    }

    static TextEditorState OpenCompletion(string text, int start, int caret)
    {
        var state = new TextEditorState(text, new LanguageServices
        {
            Completion = new FixedCompletion(new CompletionList(start, [new("alpha"), new("alpine")]))
        });
        state.Document.SetSelections([TextSelection.At(caret)]);
        state.TriggerCompletion();
        state.Update(0);
        return state;
    }

    /// <summary>Direct document edits debounce diagnostics and reject answers from older immutable versions.</summary>
    [Fact]
    public void DiagnosticsDebounceRejectStaleVersionsAndTrackEdits()
    {
        var provider = new ManualDiagnostics();
        var state = new TextEditorState("ab", new LanguageServices { Diagnostics = provider });
        state.Update(0);
        Assert.Single(provider.Requests);
        state.Document.Apply([new(2, 0, "c")], [TextSelection.At(3)]);
        state.Update(0.1);
        Assert.Single(provider.Requests);
        Assert.Equal("ab", provider.Requests[0].Snapshot.Buffer.Text);
        provider.Requests[0].Answer.SetResult([new(0, 1, DiagnosticSeverity.Error, "old")]);
        state.Update(0.2);
        Assert.Empty(state.Diagnostics);
        state.Update(0.5);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal("abc", provider.Requests[1].Snapshot.Buffer.Text);
        provider.Requests[1].Answer.SetResult([new(1, 2, DiagnosticSeverity.Warning, "new")]);
        state.Update(0.6);
        Assert.Equal("new", Assert.Single(state.Diagnostics).Message);
        Assert.False(state.IsWaitingForProviders);

        state.Document.Apply([new(0, 0, "12")], [TextSelection.At(2)]);

        Assert.Equal(new TextDiagnostic(3, 2, DiagnosticSeverity.Warning, "new"), Assert.Single(state.Diagnostics));
        var diagnostics = state.Diagnostics;
        state.Document.Undo();
        Assert.Same(diagnostics, state.Diagnostics);
    }

    /// <summary>Replacement text clears old fold headers and undo history while preserving save line endings.</summary>
    [Fact]
    public void ReplacingTextClearsFoldsCompletionAndHistory()
    {
        var state = OpenCompletion("b\nal", 2, 4);
        state.Folds.SetRanges([new(0, 1)]);
        Assert.True(state.ToggleFold(0));
        state.Document.Apply([new(4, 0, "p")], [TextSelection.At(5)]);
        Assert.True(state.Document.CanUndo);

        state.Text = "new\r\ntext";

        Assert.Equal("new\ntext", state.Text);
        Assert.Equal("new\r\ntext", state.Document.ToFileText());
        Assert.Empty(state.FoldingRanges);
        Assert.False(state.IsFolded(0));
        Assert.False(state.ToggleFold(0));
        Assert.False(state.Document.CanUndo);
        Assert.Null(state.Completion);
    }

    /// <summary>Completion follows the prefix and closes when the caret precedes the replacement range.</summary>
    [Fact]
    public void CompletionFiltersAfterDirectEdits()
    {
        var state = OpenCompletion("b\nal", 2, 4);
        Assert.Equal(2, state.Completion!.Items.Count);
        state.Document.Apply([new(4, 0, "p")], [TextSelection.At(5)]);
        state.Update(0);
        Assert.Equal(2, state.Completion!.Items.Count);
        state.Document.Apply([new(5, 0, "h")], [TextSelection.At(6)]);
        state.Update(0);
        Assert.Equal("alpha", Assert.Single(state.Completion!.Items).Label);

        state.Document.SetSelections([TextSelection.At(0)]);
        state.Update(0);

        Assert.Null(state.Completion);
    }

    /// <summary>Multiple carets, nonempty selections, another line or an unmatched prefix close completion.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CompletionClosesWhenItsContextIsLost(int context)
    {
        var state = OpenCompletion("al\nb", 0, 2);
        Assert.NotNull(state.Completion);
        switch (context)
        {
            case 0:
                state.Document.SetSelections([TextSelection.At(1), TextSelection.At(2)]);
                break;
            case 1:
                state.Document.SetSelections([new TextSelection(0, 2)]);
                break;
            case 2:
                state.Document.SetSelections([TextSelection.At(4)]);
                break;
            default:
                state.Document.Apply([new(0, 2, "zz")], [TextSelection.At(2)]);
                break;
        }

        state.Update(0);

        Assert.Null(state.Completion);
    }

    /// <summary>No-provider states and empty completion responses remain idle.</summary>
    [Fact]
    public void MissingProvidersAndEmptyCompletionsAreSafe()
    {
        var state = new TextEditorState(new StringTextBuffer("a"), new LanguageServices());
        state.TriggerCompletion();
        state.RequestHover(0);
        state.Update(0);
        Assert.Null(state.Completion);
        Assert.Null(state.Hover);
        Assert.False(state.IsWaitingForProviders);
        foreach (var list in new CompletionList?[] { null, new(0, []) })
        {
            state.Language = new LanguageServices { Completion = new FixedCompletion(list) };
            state.TriggerCompletion();
            state.Update(0);
            Assert.Null(state.Completion);
        }
        Assert.Throws<ArgumentNullException>(() => state.Language = null!);
    }

    /// <summary>Language aliases choose highlighting, folding and comment conventions.</summary>
    [Theory]
    [InlineData("csharp", "//", false)]
    [InlineData("cs", "//", false)]
    [InlineData("python", "#", true)]
    [InlineData("py", "#", true)]
    [InlineData("json", null, false)]
    public void LanguageAliasesChooseTheExpectedServices(string name, string? comment, bool indentation)
    {
        var language = LanguageServices.ForLanguage(name);
        Assert.NotNull(language.Highlighter);
        Assert.Equal(comment, language.LineComment);
        Assert.Equal(indentation, language.Folding is IndentationFoldingProvider);
        Assert.Equal(3, language.Brackets.Count);
        Assert.Equal(2, language.Quotes.Count);
        Assert.Same(LanguageServices.PlainText, LanguageServices.ForLanguage("unknown"));
        Assert.Same(LanguageServices.PlainText, LanguageServices.ForLanguage(null));
        Assert.Equal("\r\n", new TextEditorState("a\r\nb").Document.LineBreak);
    }

    /// <summary>A token cache without a highlighter emits no tokens or budget consumption.</summary>
    [Fact]
    public void PlainTextTokenizationDoesNotConsumeTheBudget()
    {
        var cache = new SyntaxTokenCache();
        var tokens = new List<SyntaxToken>();
        var budget = 0;

        Assert.True(cache.TryTokenize(new StringTextBuffer("plain"), 0, ref budget, tokens));
        Assert.Empty(tokens);
        Assert.Equal(0, budget);
    }
}

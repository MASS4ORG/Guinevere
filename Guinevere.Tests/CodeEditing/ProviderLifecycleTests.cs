namespace Guinevere.Tests.CodeEditing;

/// <summary>Checks that provider results belong to the active language and document.</summary>
public class ProviderLifecycleTests
{
    sealed class PendingProviders : IDiagnosticsProvider, IFoldingProvider, ICompletionProvider, IHoverProvider
    {
        readonly TaskCompletionSource<IReadOnlyList<TextDiagnostic>> _diagnostics = new();
        readonly TaskCompletionSource<IReadOnlyList<FoldingRange>> _folding = new();
        readonly TaskCompletionSource<CompletionList?> _completion = new();
        readonly TaskCompletionSource<HoverInfo?> _hover = new();
        internal List<CancellationToken> Tokens { get; } = [];
        internal LanguageServices Services => LanguageServices.PlainText with
        {
            Diagnostics = this,
            Folding = this,
            Completion = this,
            Hover = this,
        };

        /// <inheritdoc />
        public Task<IReadOnlyList<TextDiagnostic>> GetDiagnosticsAsync(TextSnapshot snapshot,
            CancellationToken cancellation)
        {
            Tokens.Add(cancellation);
            return _diagnostics.Task;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<FoldingRange>> GetFoldingRangesAsync(TextSnapshot snapshot,
            CancellationToken cancellation)
        {
            Tokens.Add(cancellation);
            return _folding.Task;
        }

        /// <inheritdoc />
        public Task<CompletionList?> GetCompletionsAsync(TextSnapshot snapshot, int offset,
            CancellationToken cancellation)
        {
            Tokens.Add(cancellation);
            return _completion.Task;
        }

        /// <inheritdoc />
        public Task<HoverInfo?> GetHoverAsync(TextSnapshot snapshot, int offset, CancellationToken cancellation)
        {
            Tokens.Add(cancellation);
            return _hover.Task;
        }

        internal void Complete()
        {
            _diagnostics.SetResult([new TextDiagnostic(0, 1, DiagnosticSeverity.Warning, "old language")]);
            _folding.SetResult([new FoldingRange(0, 1)]);
            _completion.SetResult(new CompletionList(0, [new CompletionItem("alphabet")]));
            _hover.SetResult(new HoverInfo(0, 1, "old hover"));
        }
    }

    static LanguageServices NoProviders => LanguageServices.PlainText with
    {
        Completion = null,
        Folding = null,
        Diagnostics = null,
        Hover = null,
    };

    static TextEditorState Start(PendingProviders providers)
    {
        var state = new TextEditorState("a\nb", providers.Services);
        state.Document.SetSelections([TextSelection.At(1)]);
        state.Update(1);
        state.TriggerCompletion();
        state.RequestHover(0);
        return state;
    }

    /// <summary>Switching languages clears every previously published result and folded region.</summary>
    [Fact]
    public void LanguageChangesClearPublishedProviderResults()
    {
        var providers = new PendingProviders();
        var state = Start(providers);
        providers.Complete();
        state.Update(2);
        Assert.Single(state.Diagnostics);
        Assert.Single(state.FoldingRanges);
        Assert.NotNull(state.Completion);
        Assert.NotNull(state.Hover);
        state.ToggleFold(0);

        state.Language = NoProviders;

        Assert.Empty(state.Diagnostics);
        Assert.Empty(state.FoldingRanges);
        Assert.False(state.IsFolded(0));
        Assert.Null(state.Hover);
        Assert.Null(state.Completion);
        Assert.False(state.IsWaitingForProviders);
    }

    /// <summary>Late answers from a replaced language are ignored even when document versions are equal.</summary>
    [Fact]
    public void LanguageChangesCancelRequestsAndDropLateAnswers()
    {
        var providers = new PendingProviders();
        var state = Start(providers);
        Assert.Equal(4, providers.Tokens.Count);
        Assert.True(state.IsWaitingForProviders);

        state.Language = NoProviders;

        Assert.All(providers.Tokens, token => Assert.True(token.IsCancellationRequested));
        providers.Complete();
        state.Update(2);
        Assert.Empty(state.Diagnostics);
        Assert.Empty(state.FoldingRanges);
        Assert.Null(state.Completion);
        Assert.Null(state.Hover);
        Assert.False(state.IsWaitingForProviders);
    }

}

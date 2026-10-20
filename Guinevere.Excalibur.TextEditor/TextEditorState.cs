namespace Guinevere;

/// <summary>
/// Everything a text editor keeps between frames: the document, the language services, settings, find, folds,
/// provider results and scroll position. Commands run against it directly, so editing can be scripted and tested
/// without drawing; <see cref="TextEditorGui.TextEditor"/> draws it and feeds it input.
/// </summary>
public sealed class TextEditorState
{
    const double DiagnosticsDelay = 0.3;
    const double FoldingDelay = 0.2;

    readonly ProviderRequest<IReadOnlyList<TextDiagnostic>> _diagnostics = new();
    readonly ProviderRequest<IReadOnlyList<FoldingRange>> _folding = new();
    readonly ProviderRequest<CompletionList?> _completion = new();
    readonly ProviderRequest<HoverInfo?> _hover = new();
    LanguageServices _language;
    int _diagnosticsVersion = -1;
    int _foldingVersion = -1;
    double _changedAt = double.NegativeInfinity;
    double _now;

    /// <summary>Creates an editor over a tree-backed buffer, suited to documents of any size.</summary>
    public TextEditorState(string text = "", LanguageServices? language = null)
        : this(new SumTreeTextBuffer(text), language)
    {
        Document.LineBreak = TextBufferText.DetectLineBreak(text);
    }

    /// <summary>Creates an editor over a buffer, such as a <see cref="StringTextBuffer"/> for small texts.</summary>
    public TextEditorState(ITextBuffer buffer, LanguageServices? language = null)
    {
        Document = new TextDocument(buffer);
        Document.Changed += OnChanged;
        _language = language ?? LanguageServices.PlainText;
        Tokens.Use(_language.Highlighter);
    }

    /// <summary>The text, selections and undo history.</summary>
    public TextDocument Document { get; }

    /// <summary>The highlighter and providers; changing it refreshes every provider result.</summary>
    public LanguageServices Language
    {
        get => _language;
        set
        {
            _language = value ?? throw new ArgumentNullException(nameof(value));
            Tokens.Use(value.Highlighter);
            _diagnosticsVersion = -1;
            _foldingVersion = -1;
            _diagnostics.Cancel();
            _folding.Cancel();
            Diagnostics = [];
            Folds.SetRanges([]);
            ClearHover();
            CloseCompletion();
        }
    }

    /// <summary>Indentation, auto-closing and other editing behavior.</summary>
    public TextEditorSettings Settings { get; set; } = new();

    /// <summary>The find and replace query.</summary>
    public FindState Find { get; } = new();

    /// <summary>Which key chords run which commands.</summary>
    public EditorKeymap Keymap { get; set; } = EditorKeymap.CreateDefault();

    /// <summary>The clipboard used by copy, cut and paste; the editor control sets the platform's when null.</summary>
    public IClipboard? Clipboard { get; set; }

    /// <summary>The whole text; setting it replaces the document and clears the undo history.</summary>
    public string Text
    {
        get => Document.Buffer.Text;
        set
        {
            Document.Load(value);
            Folds.SetRanges([]);
            CloseCompletion();
        }
    }

    /// <summary>Horizontal scroll offset in pixels.</summary>
    public float ScrollX { get; set; }

    /// <summary>Vertical scroll offset in pixels.</summary>
    public float ScrollY { get; set; }

    /// <summary>Rows that fit the viewport, used by page movement; updated by the editor control.</summary>
    public int PageRows { get; set; } = 30;

    /// <summary>Whether the editor control holds keyboard focus.</summary>
    public bool IsFocused { get; internal set; }

    /// <summary>The latest diagnostics, moved along with edits until the provider reports again.</summary>
    public IReadOnlyList<TextDiagnostic> Diagnostics { get; private set; } = [];

    /// <summary>The latest foldable regions.</summary>
    public IReadOnlyList<FoldingRange> FoldingRanges => Folds.Ranges;

    /// <summary>The open completion list, or null.</summary>
    public CompletionSession? Completion { get; private set; }

    /// <summary>The hover information shown, or null.</summary>
    public HoverInfo? Hover { get; private set; }

    /// <summary>Whether any provider request is running, so the control keeps drawing frames.</summary>
    public bool IsWaitingForProviders =>
        _diagnostics.IsPending || _folding.IsPending || _completion.IsPending || _hover.IsPending
        || (Language.Diagnostics is not null && _diagnosticsVersion != Document.Version)
        || (Language.Folding is not null && _foldingVersion != Document.Version);

    internal SyntaxTokenCache Tokens { get; } = new();

    internal FoldingState Folds { get; } = new();

    internal TextEditorViewState View { get; } = new();

    /// <summary>Set by commands that move the caret, so the control scrolls it into view.</summary>
    internal bool RevealCaret { get; set; }

    /// <summary>The line text copied by a copy of whole lines, so pasting it inserts lines above the caret.</summary>
    internal string? LineClipboard { get; set; }

    /// <summary>Runs a command by name, such as <c>deleteLeft</c> or <c>undo</c>; returns false when unknown.</summary>
    public bool Execute(string command) => EditorCommands.Run(command, this);

    /// <summary>Types text at every caret, with auto-closing and over-typing of brackets and quotes.</summary>
    public void Type(string text) => EditorCommands.Type(this, text);

    /// <summary>Folds or unfolds the region starting on a line; returns whether one starts there.</summary>
    public bool ToggleFold(int line) => Folds.Toggle(line);

    /// <summary>Whether the region starting on a line is folded.</summary>
    public bool IsFolded(int line) => Folds.Folded.ContainsKey(line);

    /// <summary>
    /// Advances time-based work: starts debounced diagnostics and folding requests and takes finished results.
    /// The editor control calls it every frame with the GUI clock.
    /// </summary>
    /// <param name="now">Seconds on a monotonic clock.</param>
    public void Update(double now)
    {
        _now = now;
        var snapshot = new TextSnapshot(Document.Buffer, Document.Version);
        TakeResults();
        RequestDiagnostics(snapshot);
        RequestFolding(snapshot);
        RefreshCompletion();
    }

    void RequestDiagnostics(TextSnapshot snapshot)
    {
        if (Language.Diagnostics is { } diagnostics
            && ShouldRequest(_diagnostics, _diagnosticsVersion, snapshot.Version, DiagnosticsDelay))
        {
            _diagnostics.Start(token => diagnostics.GetDiagnosticsAsync(snapshot, token), snapshot.Version);
        }

    }

    void RequestFolding(TextSnapshot snapshot)
    {
        if (Language.Folding is { } folding
            && ShouldRequest(_folding, _foldingVersion, snapshot.Version, FoldingDelay))
        {
            _folding.Start(token => folding.GetFoldingRangesAsync(snapshot, token), snapshot.Version);
        }
    }

    bool ShouldRequest<T>(ProviderRequest<T> request, int appliedVersion, int version, double delay) =>
        appliedVersion != version && !(request.IsPending && request.Version == version)
            && _now - _changedAt >= delay;

    /// <summary>Asks the completion provider for items at the primary caret.</summary>
    public void TriggerCompletion()
    {
        if (Language.Completion is not { } provider)
        {
            return;
        }

        var snapshot = new TextSnapshot(Document.Buffer, Document.Version);
        var offset = Document.Primary.Caret;
        _completion.Start(token => provider.GetCompletionsAsync(snapshot, offset, token), snapshot.Version, offset);
    }

    /// <summary>Closes the completion list and drops any pending request.</summary>
    public void CloseCompletion()
    {
        Completion = null;
        _completion.Cancel();
    }

    /// <summary>Asks the hover provider about an offset; the result appears in <see cref="Hover"/>.</summary>
    public void RequestHover(int offset)
    {
        if (Language.Hover is not { } provider || (_hover.IsPending && _hover.Key == offset)
            || (Hover is { } shown && offset >= shown.Start && offset < shown.Start + shown.Length))
        {
            return;
        }

        var snapshot = new TextSnapshot(Document.Buffer, Document.Version);
        _hover.Start(token => provider.GetHoverAsync(snapshot, offset, token), snapshot.Version, offset);
    }

    /// <summary>Hides the hover information and drops any pending request.</summary>
    public void ClearHover()
    {
        Hover = null;
        _hover.Cancel();
    }

    void TakeResults()
    {
        if (TryTakeCurrent(_diagnostics, out var diagnostics))
        {
            Diagnostics = diagnostics;
            _diagnosticsVersion = _diagnostics.Version;
        }

        if (TryTakeCurrent(_folding, out var ranges))
        {
            Folds.SetRanges(ranges);
            _foldingVersion = _folding.Version;
        }

        if (TryTakeCurrent(_completion, out var list) && _completion.Key == Document.Primary.Caret)
        {
            Completion = CreateCompletion(list);
        }

        if (TryTakeCurrent(_hover, out var hover))
        {
            Hover = hover;
        }
    }

    bool TryTakeCurrent<T>(ProviderRequest<T> request, out T result) =>
        request.TryTake(out result) && request.Version == Document.Version;

    static CompletionSession? CreateCompletion(CompletionList? list) =>
        list is { Items.Count: > 0 } ? new CompletionSession(list) : null;

    /// <summary>Narrows the open list to the typed prefix, closing it when the caret leaves the word.</summary>
    void RefreshCompletion()
    {
        if (Completion is not { } session)
        {
            return;
        }

        var caret = Document.Primary.Caret;
        var buffer = Document.Buffer;
        if (Document.Selections.Count != 1 || !Document.Primary.IsEmpty || caret < session.ReplaceStart
            || buffer.LineOf(caret) != buffer.LineOf(session.ReplaceStart)
            || !session.Filter(buffer.GetText(session.ReplaceStart, caret - session.ReplaceStart)))
        {
            CloseCompletion();
        }
    }

    void OnChanged(DocumentChange change)
    {
        _changedAt = _now;
        Tokens.Invalidate(change.FirstLine);
        Folds.Shift(change.FirstLine, change.LineDelta);
        Hover = null;
        Diagnostics = change.Edits.Count == 0 ? Diagnostics : [.. Diagnostics.Select(diagnostic =>
        {
            var start = TextEdit.Map(diagnostic.Start, change.Edits);
            var end = TextEdit.Map(diagnostic.Start + diagnostic.Length, change.Edits);
            return diagnostic with { Start = start, Length = Math.Max(0, end - start) };
        })];
    }
}

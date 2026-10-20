namespace Guinevere;

/// <summary>An immutable version of a document handed to language providers, safe to read on any thread.</summary>
/// <param name="Buffer">The text of the version.</param>
/// <param name="Version">The document version it was taken at.</param>
public readonly record struct TextSnapshot(ITextBuffer Buffer, int Version);

/// <summary>How serious a diagnostic is.</summary>
public enum DiagnosticSeverity
{
    /// <summary>A hint, drawn faintly.</summary>
    Hint,

    /// <summary>Information.</summary>
    Information,

    /// <summary>A warning.</summary>
    Warning,

    /// <summary>An error.</summary>
    Error,
}

/// <summary>A problem reported for a range of a snapshot.</summary>
/// <param name="Start">First offset of the range.</param>
/// <param name="Length">Length of the range; zero marks a point.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">Text shown on hover.</param>
public sealed record TextDiagnostic(int Start, int Length, DiagnosticSeverity Severity, string Message);

/// <summary>Lines that can collapse into their first line.</summary>
/// <param name="StartLine">The line that stays visible when folded.</param>
/// <param name="EndLine">The last line hidden when folded.</param>
public readonly record struct FoldingRange(int StartLine, int EndLine);

/// <summary>A completion offered at the caret.</summary>
/// <param name="Label">The text shown in the list and inserted by default.</param>
/// <param name="Kind">What it completes, shown as a hint.</param>
/// <param name="Detail">Extra text shown beside the label, or null.</param>
/// <param name="InsertText">Text inserted instead of the label, or null.</param>
public sealed record CompletionItem(string Label, SyntaxKind Kind = SyntaxKind.Plain, string? Detail = null,
    string? InsertText = null);

/// <summary>Completions for one position.</summary>
/// <param name="ReplaceStart">Start of the range an accepted item replaces, normally the word before the caret.</param>
/// <param name="Items">The items, best first.</param>
public sealed record CompletionList(int ReplaceStart, IReadOnlyList<CompletionItem> Items);

/// <summary>Information shown while the pointer rests on a range.</summary>
/// <param name="Start">First offset the information is about.</param>
/// <param name="Length">Length of that range.</param>
/// <param name="Text">The text shown, as Markdown.</param>
public sealed record HoverInfo(int Start, int Length, string Text);

/// <summary>Reports problems in a snapshot, typically by running a compiler or linter.</summary>
public interface IDiagnosticsProvider
{
    /// <summary>The diagnostics of a snapshot; cancelled when a newer version supersedes it.</summary>
    Task<IReadOnlyList<TextDiagnostic>> GetDiagnosticsAsync(TextSnapshot snapshot, CancellationToken cancellation);
}

/// <summary>Finds the foldable regions of a snapshot.</summary>
public interface IFoldingProvider
{
    /// <summary>The foldable ranges, in any order; cancelled when a newer version supersedes it.</summary>
    Task<IReadOnlyList<FoldingRange>> GetFoldingRangesAsync(TextSnapshot snapshot, CancellationToken cancellation);
}

/// <summary>Offers completions at a position.</summary>
public interface ICompletionProvider
{
    /// <summary>Completions at an offset, or null for none; cancelled when the caret or text moves on.</summary>
    Task<CompletionList?> GetCompletionsAsync(TextSnapshot snapshot, int offset, CancellationToken cancellation);
}

/// <summary>Describes what is under the pointer.</summary>
public interface IHoverProvider
{
    /// <summary>Information about an offset, or null; cancelled when the pointer moves on.</summary>
    Task<HoverInfo?> GetHoverAsync(TextSnapshot snapshot, int offset, CancellationToken cancellation);
}

/// <summary>
/// The language-specific parts of an editor. Every provider is optional and runs asynchronously against an immutable
/// snapshot, so language logic never blocks or reaches into the editor.
/// </summary>
public sealed record LanguageServices
{
    /// <summary>Plain text: no highlighting, indentation folding and word completion.</summary>
    public static LanguageServices PlainText { get; } = new()
    {
        Folding = IndentationFoldingProvider.Instance,
        Completion = WordCompletionProvider.Instance,
    };

    /// <summary>Tokenizes lines for coloring, or null for plain text.</summary>
    public ISyntaxHighlighter? Highlighter { get; init; }

    /// <summary>Reports problems, or null.</summary>
    public IDiagnosticsProvider? Diagnostics { get; init; }

    /// <summary>Finds foldable regions, or null.</summary>
    public IFoldingProvider? Folding { get; init; }

    /// <summary>Offers completions, or null.</summary>
    public ICompletionProvider? Completion { get; init; }

    /// <summary>Describes what is under the pointer, or null.</summary>
    public IHoverProvider? Hover { get; init; }

    /// <summary>The line comment marker toggled by the comment command, or null when the language has none.</summary>
    public string? LineComment { get; init; }

    /// <summary>Bracket pairs used for matching, auto-closing and indentation, as open/close characters.</summary>
    public IReadOnlyList<(char Open, char Close)> Brackets { get; init; } = [('(', ')'), ('[', ']'), ('{', '}')];

    /// <summary>Quote characters closed automatically.</summary>
    public IReadOnlyList<char> Quotes { get; init; } = ['"', '\''];

    /// <summary>Services for a language name from <see cref="SyntaxHighlighting"/>, or plain text when unknown.</summary>
    public static LanguageServices ForLanguage(string? language)
    {
        var highlighter = SyntaxHighlighting.ForLanguage(language);
        if (highlighter is null)
        {
            return PlainText;
        }

        var python = string.Equals(language, "python", StringComparison.OrdinalIgnoreCase)
            || string.Equals(language, "py", StringComparison.OrdinalIgnoreCase);
        var json = string.Equals(language, "json", StringComparison.OrdinalIgnoreCase);
        return PlainText with
        {
            Highlighter = highlighter,
            Folding = python ? IndentationFoldingProvider.Instance : BracketFoldingProvider.Instance,
            LineComment = python ? "#" : json ? null : "//",
        };
    }
}

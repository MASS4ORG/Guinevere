namespace Guinevere;

/// <summary>How <see cref="StyleSheet.Parse(string, StyleSheetOptions?)"/> reads a <c>.pss</c> sheet.</summary>
public sealed record StyleSheetOptions
{
    /// <summary>File path or name reported in parse errors, for example <c>theme.pss</c>.</summary>
    public string? SourceName { get; init; }

    /// <summary>
    /// Base for relative <c>url()</c> and <c>@font-face</c> sources: the sheet file's URI, or a directory URI
    /// ending with a separator.
    /// </summary>
    public Uri? BaseUri { get; init; }

    /// <summary>Host lookup for <c>@import "id";</c>: returns the sheet text for an id, or <c>null</c> when unknown.</summary>
    public Func<string, StyleSheetText?>? ImportResolver { get; init; }

    /// <summary>
    /// Accepts the CSS-flavored forms <c>prop: value;</c>, <c>--name: value;</c> and <c>var(--name)</c>.
    /// Intended for migration tools; shipped sheets use the PanGui <c>=</c> syntax.
    /// </summary>
    public bool AllowCssSyntax { get; init; }
}

/// <summary>Sheet text returned by a <see cref="StyleSheetOptions.ImportResolver"/>.</summary>
/// <param name="Text">The <c>.pss</c> source.</param>
/// <param name="SourceName">Name reported in errors; defaults to the import id.</param>
/// <param name="BaseUri">Base for the imported sheet's relative URLs; defaults to the importer's base.</param>
public sealed record StyleSheetText(string Text, string? SourceName = null, Uri? BaseUri = null);

/// <summary>A stylesheet parse error with the position of the offending text in its source.</summary>
public sealed class StyleSheetException : FormatException
{
    /// <summary>Creates an error formatted as <c>source:line:column: reason</c>.</summary>
    /// <param name="sourceName">File path or name of the sheet, or <c>null</c> for inline text.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    /// <param name="reason">What is wrong at that position.</param>
    public StyleSheetException(string? sourceName, int line, int column, string reason)
        : base($"{sourceName ?? "<inline>"}:{line}:{column}: {reason}")
    {
        SourceName = sourceName;
        Line = line;
        Column = column;
        Reason = reason;
    }

    /// <summary>File path or name of the sheet, or <c>null</c> for inline text.</summary>
    public string? SourceName { get; }

    /// <summary>1-based line of the error.</summary>
    public int Line { get; }

    /// <summary>1-based column of the error.</summary>
    public int Column { get; }

    /// <summary>The error description without the position prefix.</summary>
    public string Reason { get; }
}

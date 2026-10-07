namespace Guinevere;

/// <summary>A parse or reload failure that leaves the last valid stylesheet active.</summary>
/// <param name="Message">The error message, prefixed with <c>source:line:column</c> for parse errors.</param>
/// <param name="Source">File path or source name of the failing sheet, if known.</param>
/// <param name="Exception">The underlying exception.</param>
/// <param name="Line">1-based line of a parse error, or 0.</param>
/// <param name="Column">1-based column of a parse error, or 0.</param>
public sealed record StyleDiagnostic(string Message, string? Source = null, Exception? Exception = null,
    int Line = 0, int Column = 0);

/// <summary>
/// Owns a reloadable stylesheet backed by text, a file/resource identifier, or a text provider.
/// Reloads are atomic: <see cref="Current"/> changes only after a successful parse.
/// </summary>
public sealed class StyleSheetSource
{
    readonly Func<string> _provider;
    readonly string? _filePath;
    readonly StyleSheetOptions _options;
    DateTime _lastWriteTimeUtc;

    /// <summary>The last successfully parsed stylesheet.</summary>
    public StyleSheet Current { get; private set; }
    /// <summary>The most recent reload error, or <c>null</c> after a successful reload.</summary>
    public StyleDiagnostic? Diagnostic { get; private set; }
    /// <summary>Raised after <see cref="Current"/> is replaced with a valid reload.</summary>
    public event Action<StyleSheet>? Reloaded;

    StyleSheetSource(Func<string> provider, string? filePath, StyleSheetOptions options)
    {
        _provider = provider;
        _filePath = filePath;
        _options = options;
        Current = StyleSheet.Parse(provider(), options);
        if (filePath is not null) _lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
    }

    /// <summary>Creates a source containing fixed stylesheet text.</summary>
    /// <param name="text">The <c>.pss</c> source.</param>
    /// <param name="options">Source name, URL base, import resolver and syntax options.</param>
    public static StyleSheetSource FromString(string text, StyleSheetOptions? options = null) =>
        new(() => text, null, options ?? new StyleSheetOptions());

    /// <summary>Creates a source that requests fresh text from a provider on every reload.</summary>
    /// <param name="provider">Returns the current <c>.pss</c> source.</param>
    /// <param name="options">Source name, URL base, import resolver and syntax options.</param>
    public static StyleSheetSource FromProvider(Func<string> provider, StyleSheetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new StyleSheetSource(provider, null, options ?? new StyleSheetOptions());
    }

    /// <summary>
    /// Creates a reloadable UTF-8 file source. Errors report the file path, and relative <c>url()</c> values resolve
    /// against the file unless <paramref name="options"/> overrides them.
    /// </summary>
    /// <param name="path">The <c>.pss</c> file.</param>
    /// <param name="options">Import resolver and syntax options; source name and base default to the file.</param>
    public static StyleSheetSource FromFile(string path, StyleSheetOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var given = options ?? new StyleSheetOptions();
        var fileOptions = given with
        {
            SourceName = given.SourceName ?? fullPath,
            BaseUri = given.BaseUri ?? new Uri(fullPath),
        };
        return new StyleSheetSource(() => File.ReadAllText(fullPath), fullPath, fileOptions);
    }

    /// <summary>Reloads and atomically replaces <see cref="Current"/> if the new text is valid.</summary>
    public bool TryReload()
    {
        try
        {
            var parsed = StyleSheet.Parse(_provider(), _options);
            Current = parsed;
            Diagnostic = null;
            if (_filePath is not null) _lastWriteTimeUtc = File.GetLastWriteTimeUtc(_filePath);
            Reloaded?.Invoke(parsed);
            return true;
        }
        catch (StyleSheetException exception)
        {
            Diagnostic = new StyleDiagnostic(exception.Message, exception.SourceName ?? _filePath, exception,
                exception.Line, exception.Column);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            Diagnostic = new StyleDiagnostic(exception.Message, _options.SourceName ?? _filePath, exception);
            return false;
        }
    }

    /// <summary>Reloads a file source only when its last-write timestamp changed.</summary>
    public bool TryReloadIfChanged()
    {
        if (_filePath is null) return false;
        var writeTime = File.GetLastWriteTimeUtc(_filePath);
        return writeTime != _lastWriteTimeUtc && TryReload();
    }
}

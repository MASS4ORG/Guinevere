namespace Guinevere;

/// <summary>A parse or reload failure that leaves the last valid stylesheet active.</summary>
public sealed record StyleDiagnostic(string Message, string? Source = null, Exception? Exception = null);

/// <summary>
/// Owns a reloadable stylesheet backed by text, a file/resource identifier, or a text provider.
/// Reloads are atomic: <see cref="Current"/> changes only after a successful parse.
/// </summary>
public sealed class StyleSheetSource
{
    readonly Func<string> _provider;
    readonly string? _filePath;
    DateTime _lastWriteTimeUtc;

    /// <summary>The last successfully parsed stylesheet.</summary>
    public StyleSheet Current { get; private set; }
    /// <summary>The most recent reload error, or <c>null</c> after a successful reload.</summary>
    public StyleDiagnostic? Diagnostic { get; private set; }
    /// <summary>Raised after <see cref="Current"/> is replaced with a valid reload.</summary>
    public event Action<StyleSheet>? Reloaded;

    StyleSheetSource(Func<string> provider, string? filePath)
    {
        _provider = provider;
        _filePath = filePath;
        Current = StyleSheet.Parse(provider());
        if (filePath is not null) _lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
    }

    /// <summary>Creates a source containing fixed stylesheet text.</summary>
    public static StyleSheetSource FromString(string text) => new(() => text, null);

    /// <summary>Creates a source that requests fresh text from a provider on every reload.</summary>
    public static StyleSheetSource FromProvider(Func<string> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new StyleSheetSource(provider, null);
    }

    /// <summary>Creates a reloadable UTF-8 file source.</summary>
    public static StyleSheetSource FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        return new StyleSheetSource(() => File.ReadAllText(fullPath), fullPath);
    }

    /// <summary>Reloads and atomically replaces <see cref="Current"/> if the new text is valid.</summary>
    public bool TryReload()
    {
        try
        {
            var parsed = StyleSheet.Parse(_provider());
            Current = parsed;
            Diagnostic = null;
            if (_filePath is not null) _lastWriteTimeUtc = File.GetLastWriteTimeUtc(_filePath);
            Reloaded?.Invoke(parsed);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            Diagnostic = new StyleDiagnostic(exception.Message, _filePath, exception);
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

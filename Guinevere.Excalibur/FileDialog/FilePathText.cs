namespace Guinevere;

/// <summary>
/// Reads the text typed into the file dialog's path box. Turns it into an absolute folder or file path and
/// cuts it where tab completion starts, so the box accepts what a shell or file manager would.
/// </summary>
static class FilePathText
{
    /// <summary>
    /// Trims the text and strips the quotes a pasted path carries, expands a leading <c>~</c>, resolves
    /// <c>.</c>, <c>..</c> and relative paths against <paramref name="baseFolder"/> and drops a trailing separator.
    /// Text the platform cannot resolve is returned trimmed, for a provider that has its own path scheme.
    /// </summary>
    /// <param name="input">What the user typed.</param>
    /// <param name="baseFolder">The folder a relative path starts from.</param>
    /// <param name="home">The folder <c>~</c> stands for, or null for the user's profile folder.</param>
    /// <returns>The normalized path, or an empty string when the text is blank.</returns>
    public static string Normalize(string input, string baseFolder, string? home = null)
    {
        var text = TrimQuotes(input.Trim());
        if (text.Length == 0)
        {
            return "";
        }

        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0 && text[0] == '~' && (text.Length == 1 || IsSeparator(text[1])))
        {
            text = home + text[1..];
        }

        var origin = Path.IsPathFullyQualified(baseFolder) ? baseFolder : Directory.GetCurrentDirectory();
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(text, origin));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            return text;
        }
    }

    /// <summary>
    /// Splits the text at its last separator: the part that names the folder to look in, separator
    /// included, and the part being completed. A lone <c>~</c> counts as the home folder's head.
    /// </summary>
    /// <param name="text">The path as typed.</param>
    /// <returns>The folder part and the name prefix.</returns>
    public static (string Head, string Prefix) SplitLastSegment(string text)
    {
        if (text.AsSpan().Trim().SequenceEqual("~"))
        {
            return (text.TrimEnd() + Path.DirectorySeparatorChar, "");
        }

        var cut = text.AsSpan().LastIndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + 1;
        return (text[..cut], text[cut..]);
    }

    static bool IsSeparator(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;

    static string TrimQuotes(string text)
    {
        if (text.Length >= 2 && text[0] == text[^1] && (text[0] == '"' || text[0] == '\''))
        {
            return text[1..^1].Trim();
        }

        return text.Trim('"').Trim();
    }
}

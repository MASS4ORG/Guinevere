using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>A match of the find query.</summary>
/// <param name="Start">First offset.</param>
/// <param name="Length">Length in code units.</param>
public readonly record struct FindMatch(int Start, int Length)
{
    /// <summary>Offset after the match.</summary>
    public int End => Start + Length;
}

/// <summary>
/// The find and replace query of an editor and its matches, computed once per document version and query.
/// </summary>
public sealed class FindState
{
    /// <summary>The most matches reported, so a one-letter query on a huge file stays cheap.</summary>
    public const int MaxMatches = 10_000;

    static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    (int Version, string Query, bool Case, bool Word, bool Regex) _key = (-1, "", false, false, false);
    List<FindMatch> _matches = [];

    /// <summary>The text or pattern searched for.</summary>
    public string Query { get; set; } = "";

    /// <summary>The replacement; with <see cref="UseRegex"/> it may refer to groups as <c>$1</c>.</summary>
    public string Replacement { get; set; } = "";

    /// <summary>Whether letter case must match.</summary>
    public bool MatchCase { get; set; }

    /// <summary>Whether a match must be a whole identifier.</summary>
    public bool WholeWord { get; set; }

    /// <summary>Whether <see cref="Query"/> is a regular expression.</summary>
    public bool UseRegex { get; set; }

    /// <summary>Whether the find bar is shown.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Whether the replace field is shown.</summary>
    public bool ShowReplace { get; set; }

    /// <summary>The error of an invalid pattern, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>The matches in a document version, sorted by start.</summary>
    public IReadOnlyList<FindMatch> Matches(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var key = (document.Version, Query, MatchCase, WholeWord, UseRegex);
        if (key == _key)
        {
            return _matches;
        }

        _key = key;
        Error = null;
        _matches = Query.Length == 0 ? [] : Search(document.Buffer.Text);
        return _matches;
    }

    /// <summary>The replacement text for a match, expanding groups for a regular expression.</summary>
    public string ReplacementFor(TextDocument document, FindMatch match)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!UseRegex || Pattern() is not { } regex)
        {
            return Replacement;
        }

        var found = regex.Match(document.Buffer.Text, match.Start);
        return found.Success && found.Index == match.Start ? found.Result(Replacement) : Replacement;
    }

    /// <summary>One edit per match replacing it, computed in a single pass over the text.</summary>
    public IReadOnlyList<TextEdit> ReplaceAllEdits(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var matches = Matches(document);
        if (matches.Count == 0)
        {
            return [];
        }

        if (!UseRegex || Pattern() is not { } regex)
        {
            return [.. matches.Select(match => new TextEdit(match.Start, match.Length, Replacement))];
        }

        var text = document.Buffer.Text;
        return [.. matches.Select(match => new TextEdit(match.Start, match.Length,
            regex.Match(text, match.Start) is { Success: true } found ? found.Result(Replacement) : Replacement))];
    }

    List<FindMatch> Search(string text)
    {
        var matches = new List<FindMatch>();
        if (Pattern() is not { } regex)
        {
            return matches;
        }

        try
        {
            for (var match = regex.Match(text); match.Success && matches.Count < MaxMatches; match = match.NextMatch())
            {
                if (match.Length > 0)
                {
                    matches.Add(new FindMatch(match.Index, match.Length));
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            Error = "The pattern took too long.";
        }

        return matches;
    }

    Regex? Pattern()
    {
        var pattern = UseRegex ? Query : Regex.Escape(Query);
        if (WholeWord)
        {
            pattern = $@"(?<![\w]){pattern}(?![\w])";
        }

        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant
            | (MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            return new Regex(pattern, options, RegexTimeout);
        }
        catch (ArgumentException exception)
        {
            Error = exception.Message;
            return null;
        }
    }
}

namespace Guinevere;

/// <summary>
/// What the file dialog remembers about one stretch of path editing: where it began, whether the box has had
/// focus yet, and the tab-completion matches being cycled through. It lives from the first frame the box is shown
/// until the bar returns to crumbs.
/// </summary>
sealed class PathEditSession(string origin, bool selectAll) : IDisposable
{
    CancellationTokenSource? _query;

    /// <summary>The folder the browser showed when editing began; landing in another one ends the edit.</summary>
    public string Origin { get; } = origin;

    /// <summary>Whether the whole path is selected when the box gets focus, rather than the caret put at its end.</summary>
    public bool SelectAll { get; set; } = selectAll;

    /// <summary>Whether the box still has to be given focus, which is requested every frame until it lands.</summary>
    public bool Grab { get; set; } = true;

    /// <summary>Whether the box has held focus since it was shown; losing it afterwards ends the edit.</summary>
    public bool FocusSeen { get; set; }

    /// <summary>Whether the last attempt to go to the typed path failed.</summary>
    public bool Failed { get; set; }

    /// <summary>Whether the status line currently shows a completion hint rather than an error.</summary>
    public bool HintShown { get; set; }

    /// <summary>The text up to and including the separator before the segment being completed.</summary>
    public string Head { get; private set; } = "";

    /// <summary>The folder names matching the segment, in the order Tab visits them.</summary>
    public IReadOnlyList<string> Matches { get; private set; } = [];

    /// <summary>The index into <see cref="Matches"/> currently written into the box, or -1 for none.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>The text the last Tab wrote, so another Tab on unchanged text cycles instead of searching again.</summary>
    public string? Produced { get; private set; }

    /// <summary>Starts a completion query, cancelling the one still running.</summary>
    /// <returns>The new query's token source.</returns>
    public CancellationTokenSource StartQuery()
    {
        _query?.Cancel();
        _query?.Dispose();
        _query = new CancellationTokenSource();
        return _query;
    }

    /// <summary>Takes <paramref name="matches"/> as the set to cycle through, starting at the first.</summary>
    public string Begin(string head, IReadOnlyList<string> matches)
    {
        Head = head;
        Matches = matches;
        Index = 0;
        return Produced = Head + matches[0] + Path.DirectorySeparatorChar;
    }

    /// <summary>Moves to the next or previous match, wrapping around.</summary>
    /// <returns>The text to write into the box.</returns>
    public string Step(bool backwards)
    {
        Index = (Index + (backwards ? Matches.Count - 1 : 1)) % Matches.Count;
        return Produced = Head + Matches[Index] + Path.DirectorySeparatorChar;
    }

    /// <summary>Whether <paramref name="text"/> is what the last Tab wrote and matches are left to cycle.</summary>
    public bool IsCycling(string text) => Index >= 0 && string.Equals(text, Produced, StringComparison.Ordinal);

    /// <summary>Forgets the matches, because the user typed something else.</summary>
    public void ResetCycle()
    {
        Matches = [];
        Index = -1;
        Produced = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _query?.Cancel();
        _query?.Dispose();
        _query = null;
    }
}

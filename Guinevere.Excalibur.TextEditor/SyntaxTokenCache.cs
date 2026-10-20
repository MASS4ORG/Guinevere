namespace Guinevere;

/// <summary>
/// The highlighter state at the start of each line, kept across frames and edits. An edit drops only the states
/// after its first line, and tokenizing forward is spread over frames by a line budget, so jumping deep into a large
/// document never stalls a frame.
/// </summary>
sealed class SyntaxTokenCache
{
    readonly List<int> _states = [0];
    readonly List<SyntaxToken> _scratch = [];
    ISyntaxHighlighter? _highlighter;

    /// <summary>Number of lines whose start state is known.</summary>
    internal int KnownLines => _states.Count;

    /// <summary>Forgets every state when the highlighter changes.</summary>
    internal void Use(ISyntaxHighlighter? highlighter)
    {
        if (!ReferenceEquals(highlighter, _highlighter))
        {
            _highlighter = highlighter;
            Invalidate(0);
        }
    }

    /// <summary>Drops the states after <paramref name="line"/>, whose start state still holds.</summary>
    internal void Invalidate(int line)
    {
        var keep = Math.Max(1, line + 1);
        if (keep < _states.Count)
        {
            _states.RemoveRange(keep, _states.Count - keep);
        }
    }

    /// <summary>
    /// Appends the tokens of a line, tokenizing the lines before it within <paramref name="budget"/> lines.
    /// Returns false, adding nothing, when the budget runs out first.
    /// </summary>
    internal bool TryTokenize(ITextBuffer buffer, int line, ref int budget, List<SyntaxToken> tokens)
    {
        if (_highlighter is not { } highlighter)
        {
            return true;
        }

        while (_states.Count <= line)
        {
            if (budget <= 0)
            {
                return false;
            }

            var known = _states.Count - 1;
            _scratch.Clear();
            _states.Add(highlighter.Tokenize(buffer.Line(known), _states[known], _scratch));
            budget--;
        }

        highlighter.Tokenize(buffer.Line(line), _states[line], tokens);
        return true;
    }
}

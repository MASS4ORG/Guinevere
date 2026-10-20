namespace Guinevere;

/// <summary>Folds blocks whose following lines are indented deeper, as in Python or YAML.</summary>
public sealed class IndentationFoldingProvider : IFoldingProvider
{
    /// <summary>The shared instance.</summary>
    public static IndentationFoldingProvider Instance { get; } = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<FoldingRange>> GetFoldingRangesAsync(TextSnapshot snapshot,
        CancellationToken cancellation) => Task.Run(() => Find(snapshot.Buffer, cancellation), cancellation);

    /// <summary>The ranges of a buffer, computed on the calling thread.</summary>
    public static IReadOnlyList<FoldingRange> Find(ITextBuffer buffer, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var indents = new int[buffer.LineCount];
        for (var line = 0; line < indents.Length; line++)
        {
            cancellation.ThrowIfCancellationRequested();
            var text = buffer.Line(line);
            indents[line] = string.IsNullOrWhiteSpace(text) ? -1 : TextNavigation.VisualColumn(text,
                TextNavigation.Indentation(text).Length, 4);
        }

        var ranges = new List<FoldingRange>();
        var open = new Stack<(int Line, int Indent)>();
        var lastContent = -1;
        for (var line = 0; line < indents.Length; line++)
        {
            if (indents[line] < 0)
            {
                continue;
            }

            while (open.Count > 0 && indents[line] <= open.Peek().Indent)
            {
                Close(open.Pop().Line, lastContent, ranges);
            }

            open.Push((line, indents[line]));
            lastContent = line;
        }

        while (open.Count > 0)
        {
            Close(open.Pop().Line, lastContent, ranges);
        }

        return ranges;
    }

    static void Close(int start, int end, List<FoldingRange> ranges)
    {
        if (end > start)
        {
            ranges.Add(new FoldingRange(start, end));
        }
    }
}

/// <summary>
/// Folds the lines between an opening bracket and its closing one, skipping brackets inside C-style strings and
/// comments. The line with the closing bracket stays visible.
/// </summary>
public sealed class BracketFoldingProvider : IFoldingProvider
{
    /// <summary>The shared instance.</summary>
    public static BracketFoldingProvider Instance { get; } = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<FoldingRange>> GetFoldingRangesAsync(TextSnapshot snapshot,
        CancellationToken cancellation) => Task.Run(() => Find(snapshot.Buffer.Text, cancellation), cancellation);

    /// <summary>The ranges of a text, computed on the calling thread.</summary>
    public static IReadOnlyList<FoldingRange> Find(string text, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ranges = new List<FoldingRange>();
        var open = new Stack<(char Bracket, int Line)>();
        var line = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if ((i & 0xffff) == 0)
            {
                cancellation.ThrowIfCancellationRequested();
            }

            if (!SkipNonCode(text, ref i, ref line))
            {
                ReadBracket(text[i], line, open, ranges);
            }
        }

        return ranges;
    }

    static bool SkipNonCode(string text, ref int offset, ref int line)
    {
        var c = text[offset];
        if (c == '\n')
        {
            line++;
            return true;
        }
        if ("\"'`".Contains(c))
        {
            offset = SkipString(text, offset);
            return true;
        }
        if (IsComment(text, offset))
        {
            offset = SkipComment(text, offset, ref line);
            return true;
        }
        return false;
    }

    static bool IsComment(string text, int offset) => text[offset] == '/' && offset + 1 < text.Length
        && text[offset + 1] is '/' or '*';

    static void ReadBracket(char bracket, int line, Stack<(char Bracket, int Line)> open,
        List<FoldingRange> ranges)
    {
        if ("{[(".Contains(bracket))
        {
            open.Push((bracket, line));
            return;
        }
        var opening = bracket switch { '}' => '{', ']' => '[', ')' => '(', _ => '\0' };
        if (open.Count == 0 || open.Peek().Bracket != opening)
        {
            return;
        }
        var start = open.Pop().Line;
        if (line - 1 > start)
        {
            ranges.Add(new FoldingRange(start, line - 1));
        }
    }

    /// <summary>The offset of the quote closing the string opened at <paramref name="start"/>, or its line's end.</summary>
    static int SkipString(string text, int start)
    {
        var quote = text[start];
        for (var i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\')
            {
                i++;
            }
            else if (text[i] == quote)
            {
                return i;
            }
            else if (text[i] == '\n')
            {
                return i - 1;
            }
        }

        return text.Length;
    }

    static int SkipComment(string text, int start, ref int line)
    {
        if (text[start + 1] == '/')
        {
            var end = text.IndexOf('\n', start);
            return end < 0 ? text.Length : end - 1;
        }

        for (var i = start + 2; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
            else if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/')
            {
                return i + 1;
            }
        }

        return text.Length;
    }
}

/// <summary>Completes the word before the caret with other words of the document.</summary>
public sealed class WordCompletionProvider : ICompletionProvider
{
    const int MaxItems = 50;
    const int WindowLines = 5000;

    /// <summary>The shared instance.</summary>
    public static WordCompletionProvider Instance { get; } = new();

    /// <inheritdoc />
    public Task<CompletionList?> GetCompletionsAsync(TextSnapshot snapshot, int offset,
        CancellationToken cancellation) => Task.Run(() => Complete(snapshot.Buffer, offset), cancellation);

    /// <summary>The completions at an offset, computed on the calling thread; null when no word is being typed.</summary>
    public static CompletionList? Complete(ITextBuffer buffer, int offset)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var (start, _) = TextNavigation.WordAt(buffer, offset);
        if (start >= offset)
        {
            return null;
        }

        var prefix = buffer.GetText(start, offset - start);
        var (from, text) = Window(buffer, offset);
        var counts = CountMatches(text, from, start, prefix);
        var items = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(MaxItems).Select(pair => new CompletionItem(pair.Key)).ToList();
        return items.Count == 0 ? null : new CompletionList(start, items);
    }

    static (int From, string Text) Window(ITextBuffer buffer, int offset)
    {
        var line = buffer.LineOf(offset);
        var first = Math.Max(0, line - WindowLines);
        var last = Math.Min(buffer.LineCount - 1, line + WindowLines);
        var from = buffer.LineStart(first);
        return (from, buffer.GetText(from, buffer.LineStart(last) + buffer.LineLength(last) - from));
    }

    static Dictionary<string, int> CountMatches(string text, int from, int start, string prefix)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < text.Length; i++)
        {
            if (!TextNavigation.IsWordChar(text[i]))
            {
                continue;
            }

            var end = WordEnd(text, i);
            if (IsMatch(text, i, end, from + i == start, prefix))
            {
                var word = text[i..end];
                counts[word] = counts.GetValueOrDefault(word) + 1;
            }

            i = end;
        }

        return counts;
    }

    static bool IsMatch(string text, int start, int end, bool currentWord, string prefix) =>
        !currentWord && end - start > prefix.Length
            && text.AsSpan(start, prefix.Length).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    static int WordEnd(string text, int start)
    {
        var end = start;
        while (end < text.Length && TextNavigation.IsWordChar(text[end]))
        {
            end++;
        }
        return end;
    }
}

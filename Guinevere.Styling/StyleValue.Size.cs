using System.Globalization;

namespace Guinevere;

public static partial class StyleValue
{
    /// <summary>
    /// Parses a <see cref="Size"/>: terms <c>N</c> or <c>Npx</c> (pixels), <c>N%</c> (percentage), <c>ratio(N)</c>,
    /// <c>expand</c> or <c>expand(N)</c>, <c>fit</c>/<c>auto</c> or <c>fit(N)</c> and <c>fit-largest</c> or
    /// <c>fit-largest(N)</c>, joined by space-separated <c>+</c> and <c>-</c>, such as <c>50% + 8px</c>.
    /// </summary>
    /// <param name="text">The size text.</param>
    /// <param name="value">The parsed size.</param>
    public static bool TrySize(string? text, out Size value)
    {
        var tokens = Tokens(text.AsSpan());
        return TryReadSize(ref tokens, out value) && !NextToken(ref tokens, out _);
    }

    /// <summary>
    /// Parses up to <paramref name="values"/>.Length space-separated sizes, as in a box shorthand such as
    /// <c>8px 50% + 4px</c>; returns how many were read, or 0 when the text is invalid or has too many.
    /// </summary>
    /// <param name="text">The shorthand text.</param>
    /// <param name="values">Receives the sizes in order.</param>
    public static int ReadSizes(string? text, Span<Size> values)
    {
        var tokens = Tokens(text.AsSpan());
        var count = 0;
        for (var probe = tokens; NextToken(ref probe, out _); probe = tokens)
        {
            if (count == values.Length || !TryReadSize(ref tokens, out values[count]))
            {
                return 0;
            }

            count++;
        }

        return count;
    }

    static MemoryExtensions.SpanSplitEnumerator<char> Tokens(ReadOnlySpan<char> text) =>
        text.Trim().SplitAny(" \t\n\r");

    /// <summary>Reads one size expression: a term and any <c>+</c>/<c>-</c> terms after it.</summary>
    static bool TryReadSize(ref MemoryExtensions.SpanSplitEnumerator<char> tokens, out Size value)
    {
        value = default;
        if (!NextToken(ref tokens, out var first) || !TryTerm(first, out value))
        {
            return false;
        }

        var lookahead = tokens;
        while (NextToken(ref lookahead, out var op) && op is "+" or "-")
        {
            if (!NextToken(ref lookahead, out var operand) || !TryTerm(operand, out var term))
            {
                return false;
            }

            value = op[0] == '+' ? value + term : value - term;
            tokens = lookahead;
        }

        return true;
    }

    static bool NextToken(ref MemoryExtensions.SpanSplitEnumerator<char> tokens, out ReadOnlySpan<char> token)
    {
        while (tokens.MoveNext())
        {
            token = tokens.Source[tokens.Current];
            if (!token.IsEmpty)
            {
                return true;
            }
        }

        token = default;
        return false;
    }

    static bool TryTerm(ReadOnlySpan<char> term, out Size value)
    {
        value = default;
        if (TryLength(term, out var length, out var isPercent))
        {
            value = isPercent ? Size.Percentage(length) : Size.Pixels(length);
            return true;
        }

        if (term is "expand" or "fit" or "auto" or "fit-largest")
        {
            value = Keyword(term, 1f);
            return true;
        }

        var open = term.IndexOf('(');
        if (open <= 0 || term[^1] != ')'
            || !float.TryParse(term[(open + 1)..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
        {
            return false;
        }

        var name = term[..open];
        if (name is not ("expand" or "fit" or "fit-largest" or "ratio"))
        {
            return false;
        }

        value = Keyword(name, weight);
        return true;
    }

    static Size Keyword(ReadOnlySpan<char> name, float weight) => name switch
    {
        "expand" => Size.Expand(weight),
        "fit-largest" => Size.FitLargest(weight),
        "ratio" => Size.Ratio(weight),
        _ => Size.FitContent(weight),
    };
}

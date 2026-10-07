using System.Globalization;

namespace Guinevere;

enum StyleValueKind { Number, Color, Text, Unknown }

/// <summary>
/// An evaluated operand. <see cref="StyleValueKind.Unknown"/> stands for text that cannot be evaluated yet, such as an
/// unresolved <c>$token</c>; <see cref="Changed"/> marks results whose canonical text differs from the source.
/// </summary>
readonly record struct StyleExprValue(StyleValueKind Kind, double Number = 0d,
    StyleNumberUnit Unit = StyleNumberUnit.None, StyleColor Color = default, string? Text = null, bool Changed = false)
{
    public static StyleExprValue Unknown => new(StyleValueKind.Unknown);

    public static StyleExprValue Of(double number, StyleNumberUnit unit = StyleNumberUnit.None) =>
        new(StyleValueKind.Number, number, unit, Changed: true);

    public static StyleExprValue Of(StyleColor color) => new(StyleValueKind.Color, Color: color, Changed: true);

    public string Canonical() => Kind switch
    {
        StyleValueKind.Number => Format(Number) + (Unit == StyleNumberUnit.Percent ? "%" : string.Empty),
        StyleValueKind.Color => Color.ToHex(),
        _ => Text ?? string.Empty,
    };

    static string Format(double number) =>
        Math.Abs(number) < 1e-9 ? "0" : number.ToString("0.######", CultureInfo.InvariantCulture);
}

/// <summary>An invalid <c>.pss</c> value expression, with the offset of the problem inside the value.</summary>
sealed class StyleExpressionException(string message, int position) : FormatException(message)
{
    public int Position { get; } = position;
}

/// <summary>
/// Evaluates <c>.pss</c> value expressions: arithmetic, <c>calc()</c>, <c>em</c> lengths and the color functions in
/// <see cref="StyleFunctions"/>. Only the evaluated parts of a value are rewritten; the rest keeps its source text.
/// </summary>
sealed class StyleExpression
{
    const double DefaultEm = 16d;
    readonly string _text;
    readonly List<StyleToken> _tokens;
    readonly Func<string, string?>? _lookup;
    readonly List<(int Start, int End, string Text)> _replacements = [];
    readonly int _depth;
    double? _em;
    int _pos;

    StyleExpression(string text, Func<string, string?>? lookup, int depth)
    {
        _text = text;
        _tokens = StyleExpressionLexer.Tokenize(text);
        _lookup = lookup;
        _depth = depth;
    }

    bool Validating => _lookup is null;

    /// <summary>
    /// Evaluates every expression in an expanded value. Returns <paramref name="value"/> itself when nothing needs
    /// evaluating or evaluation fails, so a bad value falls back exactly as an unparseable literal does.
    /// </summary>
    /// <param name="value">A declaration value with tokens already substituted.</param>
    /// <param name="lookup">Token lookup (<c>--name</c>), used for <c>$font-size</c> and <c>$contrast</c>.</param>
    public static string Evaluate(string value, Func<string, string?> lookup) => Evaluate(value, lookup, 0);

    /// <summary>Checks expression syntax and the types that are already known; tokens count as unknown values.</summary>
    /// <exception cref="StyleExpressionException">The value is not a valid expression.</exception>
    public static void Validate(string value)
    {
        if (NeedsEvaluation(value)) new StyleExpression(value, null, 0).Run();
    }

    /// <summary>Whether a value contains an operator, a call, a group or an <c>em</c> length outside strings.</summary>
    public static bool NeedsEvaluation(ReadOnlySpan<char> value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is '"' or '\'') i = SkipQuoted(value, i);
            else if (Triggers(value, i)) return true;
        }
        return false;
    }

    static bool Triggers(ReadOnlySpan<char> value, int i) =>
        value[i] is '(' or '*' or '/' or '+' || value[i] == '-' && IsBinaryDash(value, i) || IsEmUnit(value, i);

    static string Evaluate(string value, Func<string, string?> lookup, int depth)
    {
        if (!NeedsEvaluation(value)) return value;
        try
        {
            return new StyleExpression(value, lookup, depth).Run();
        }
        catch (StyleExpressionException)
        {
            return value;
        }
    }

    string Run()
    {
        ParseList(topLevel: true);
        if (_pos < _tokens.Count) throw Error($"Unexpected '{Slice(_tokens[_pos])}'");
        return Apply();
    }

    /// <summary>Splices the outermost changed spans into the source text.</summary>
    string Apply()
    {
        if (_replacements.Count == 0) return _text;
        _replacements.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End));
        var builder = new System.Text.StringBuilder(_text.Length);
        var copied = 0;
        foreach (var (start, end, text) in _replacements)
        {
            if (start < copied) continue;
            builder.Append(_text, copied, start - copied).Append(text);
            copied = end;
        }
        return builder.Append(_text, copied, _text.Length - copied).ToString();
    }

    /// <summary>Parses space- or comma-separated items; returns the single item, or the list as text.</summary>
    StyleExprValue ParseList(bool topLevel)
    {
        var listStart = _pos;
        var count = 0;
        var single = StyleExprValue.Unknown;
        while (_pos < _tokens.Count && !EndsList(_tokens[_pos], topLevel))
        {
            if (topLevel && _tokens[_pos].Kind == StyleTokenKind.Comma) { _pos++; continue; }
            var start = _tokens[_pos].Start;
            single = ParseSum();
            count++;
            Record(single, start, _tokens[_pos - 1].End);
        }
        if (count == 0) throw Error("Expected a value");
        return count == 1 ? single : ListText(listStart);
    }

    static bool EndsList(StyleToken token, bool topLevel) =>
        token.Kind == StyleTokenKind.Close || !topLevel && token.Kind == StyleTokenKind.Comma;

    StyleExprValue ListText(int firstToken) =>
        new(StyleValueKind.Text, Text: _text[_tokens[firstToken].Start.._tokens[_pos - 1].End]);

    void Record(StyleExprValue value, int start, int end)
    {
        if (value.Changed && value.Kind != StyleValueKind.Unknown) _replacements.Add((start, end, value.Canonical()));
    }

    StyleExprValue ParseSum()
    {
        var left = ParseProduct();
        while (PeekOperator() is '+' or '-')
        {
            var at = _tokens[_pos++].Start;
            left = StyleFunctions.Arithmetic(_text[at], left, ParseProduct(), at);
        }
        return left;
    }

    StyleExprValue ParseProduct()
    {
        var left = ParseUnary();
        while (PeekOperator() is '*' or '/')
        {
            var at = _tokens[_pos++].Start;
            left = StyleFunctions.Arithmetic(_text[at], left, ParseUnary(), at);
        }
        return left;
    }

    StyleExprValue ParseUnary()
    {
        var negate = _pos < _tokens.Count && _tokens[_pos].Kind == StyleTokenKind.Negate || PeekOperator() == '-';
        if (!negate) return ParsePrimary();
        var at = _tokens[_pos++].Start;
        return StyleFunctions.Arithmetic('*', ParseUnary(), new StyleExprValue(StyleValueKind.Number, -1d), at);
    }

    char PeekOperator() =>
        _pos < _tokens.Count && _tokens[_pos].Kind == StyleTokenKind.Operator ? _text[_tokens[_pos].Start] : '\0';

    StyleExprValue ParsePrimary()
    {
        if (_pos >= _tokens.Count) throw Error("Unexpected end of value");
        var token = _tokens[_pos++];
        return token.Kind switch
        {
            StyleTokenKind.Number => NumberValue(token),
            StyleTokenKind.Color => new StyleExprValue(StyleValueKind.Color,
                Color: StyleColor.From(Guinevere.Color.ParseHex(Slice(token)))),
            StyleTokenKind.Word => IsCall(token) ? ParseCall(token) : Text(token),
            StyleTokenKind.Opaque => IsReference(token) ? StyleExprValue.Unknown : Text(token),
            StyleTokenKind.Open => ParseGroup(token),
            _ => throw Error($"Unexpected '{Slice(token)}'", token.Start),
        };
    }

    StyleExprValue NumberValue(StyleToken token) => token.Unit == StyleNumberUnit.Em
        ? StyleExprValue.Of(token.Number * Em())
        : new StyleExprValue(StyleValueKind.Number, token.Number, token.Unit);

    StyleExprValue Text(StyleToken token) => new(StyleValueKind.Text, Text: Slice(token));

    /// <summary>A <c>$token</c>, <c>@constant</c> or <c>--custom</c> reference, whose type is not known yet.</summary>
    bool IsReference(StyleToken token) => _text[token.Start] is '$' or '@' || _text[token.Start] == '-';

    bool IsCall(StyleToken word) =>
        _pos < _tokens.Count && _tokens[_pos].Kind == StyleTokenKind.Open && _tokens[_pos].Start == word.End;

    StyleExprValue ParseCall(StyleToken name)
    {
        var args = ParseArguments(_tokens[_pos++]);
        var function = Slice(name).ToLowerInvariant();
        return StyleFunctions.IsKnown(function)
            ? StyleFunctions.Invoke(function, args, Contrast, name.Start)
            : StyleExprValue.Unknown;
    }

    /// <summary>A parenthesised group is a single value; with commas it is a tuple such as <c>(0, 5)</c>.</summary>
    StyleExprValue ParseGroup(StyleToken open)
    {
        var items = ParseArguments(open);
        return items.Count == 1
            ? items[0]
            : new StyleExprValue(StyleValueKind.Text, Text: _text[open.Start.._tokens[_pos - 1].End]);
    }

    List<StyleExprValue> ParseArguments(StyleToken open)
    {
        var args = new List<StyleExprValue>();
        while (_pos < _tokens.Count && _tokens[_pos].Kind != StyleTokenKind.Close)
        {
            args.Add(ParseList(topLevel: false));
            if (_pos < _tokens.Count && _tokens[_pos].Kind == StyleTokenKind.Comma) _pos++;
        }
        if (_pos >= _tokens.Count) throw Error("Missing ')'", open.Start);
        _pos++;
        return args;
    }

    double Em() => _em ??= TokenNumber("--font-size", DefaultEm);

    double Contrast() => TokenNumber("--contrast", StyleFunctions.DefaultContrast);

    /// <summary>A numeric token value, evaluated with default <c>em</c> to stop self-reference.</summary>
    double TokenNumber(string name, double fallback)
    {
        if (Validating || _depth > 0 || _lookup!(name) is not { } raw) return fallback;
        var value = Evaluate(StyleSheet.Expand(raw, _lookup), _lookup, _depth + 1);
        return StyleValue.TryFloat(value, out var number) ? number : fallback;
    }

    string Slice(StyleToken token) => _text[token.Start..token.End];

    StyleExpressionException Error(string message, int? position = null) =>
        new(message, position ?? (_pos < _tokens.Count ? _tokens[_pos].Start : _text.Length));

    static int SkipQuoted(ReadOnlySpan<char> value, int start)
    {
        for (var i = start + 1; i < value.Length; i++)
        {
            if (value[i] == '\\') i++;
            else if (value[i] == value[start]) return i;
        }
        return value.Length;
    }

    static bool IsBinaryDash(ReadOnlySpan<char> value, int i) =>
        i > 0 && i + 1 < value.Length
              && (char.IsWhiteSpace(value[i - 1]) && char.IsWhiteSpace(value[i + 1])
                  || char.IsAsciiDigit(value[i - 1]) && !char.IsLetter(value[i + 1]));

    static bool IsEmUnit(ReadOnlySpan<char> value, int i) =>
        value[i] == 'e' && i > 0 && char.IsAsciiDigit(value[i - 1]) && i + 1 < value.Length && value[i + 1] == 'm'
        && (i + 2 == value.Length || !char.IsLetterOrDigit(value[i + 2]));
}

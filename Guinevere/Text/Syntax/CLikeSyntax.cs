using System.Collections.Frozen;

namespace Guinevere;

/// <summary>
/// A configurable tokenizer for brace-and-semicolon languages: keywords, types, constants, line and block comments,
/// quoted and multi-line strings, numbers, directives, function calls and operators. Block comments and multi-line
/// strings carry over lines through the returned state.
/// </summary>
public sealed class CLikeSyntax : ISyntaxHighlighter
{
    const int Code = 0;
    const int InBlockComment = 1;
    const int InMultilineString = 2;
    const string Operators = "+-*/%=<>!&|^~?:";
    const string Punctuation = "(){}[];,.";

    readonly FrozenSet<string> _keywords;
    readonly FrozenSet<string> _types;
    readonly FrozenSet<string> _constants;
    readonly string? _lineComment;
    readonly string? _blockOpen;
    readonly string? _blockClose;
    readonly string? _multilineString;
    readonly string _quotes;
    readonly char? _directive;
    readonly bool _capitalizedTypes;

    /// <summary>Creates a tokenizer for one language.</summary>
    /// <param name="keywords">Reserved words.</param>
    /// <param name="types">Built-in type names.</param>
    /// <param name="constants">Named constants such as <c>true</c>.</param>
    /// <param name="lineComment">Starts a comment to the end of the line, or null.</param>
    /// <param name="blockOpen">Opens a comment that may span lines, or null for none.</param>
    /// <param name="blockClose">Closes a comment opened by <paramref name="blockOpen"/>.</param>
    /// <param name="quotes">Characters that open and close one-line strings.</param>
    /// <param name="multilineString">Opens and closes a string that may span lines, or null.</param>
    /// <param name="directive">A character that, first on a line, makes the line a directive, or null.</param>
    /// <param name="capitalizedTypes">Whether capitalized identifiers are taken as type names.</param>
    public CLikeSyntax(IEnumerable<string> keywords, IEnumerable<string>? types = null,
        IEnumerable<string>? constants = null, string? lineComment = "//", string? blockOpen = "/*",
        string blockClose = "*/", string quotes = "\"'", string? multilineString = null, char? directive = '#',
        bool capitalizedTypes = true)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        ArgumentNullException.ThrowIfNull(quotes);
        _keywords = keywords.ToFrozenSet(StringComparer.Ordinal);
        _types = (types ?? []).ToFrozenSet(StringComparer.Ordinal);
        _constants = (constants ?? []).ToFrozenSet(StringComparer.Ordinal);
        _lineComment = lineComment;
        _blockOpen = blockOpen;
        _blockClose = blockClose;
        _quotes = quotes;
        _multilineString = multilineString;
        _directive = directive;
        _capitalizedTypes = capitalizedTypes;
    }

    /// <summary>C#, including raw string literals.</summary>
    public static CLikeSyntax CSharp { get; } = new(
        Words("abstract as async await base break case catch checked class const continue default delegate do else "
            + "enum event explicit extern finally fixed for foreach get global goto if implicit in init interface "
            + "internal is lock nameof namespace new not operator or and out override params partial private "
            + "protected public readonly record ref required return scoped sealed set sizeof stackalloc static "
            + "struct switch this throw try typeof unchecked unsafe using value var virtual volatile when where while "
            + "with yield"),
        Words("bool byte char decimal double dynamic float int long nint nuint object sbyte short string uint ulong "
            + "ushort void"),
        Words("true false null"), multilineString: "\"\"\"");

    /// <summary>JavaScript and TypeScript, including template strings.</summary>
    public static CLikeSyntax JavaScript { get; } = new(
        Words("abstract as async await break case catch class const continue debugger declare default delete do "
            + "else enum export extends finally for from function get if implements import in instanceof interface "
            + "let namespace new of private protected public readonly return set static super switch this throw try "
            + "type typeof var void while with yield"),
        Words("any bigint boolean never number object string symbol unknown"),
        Words("true false null undefined NaN Infinity"), multilineString: "`");

    /// <summary>GLSL, HLSL and SkSL shaders.</summary>
    public static CLikeSyntax Shader { get; } = new(
        Words("attribute break case cbuffer const continue default discard do else for highp if in inout layout "
            + "lowp mediump out precision register return static struct switch uniform varying while"),
        Words("blender bool bvec2 bvec3 bvec4 colorFilter double float float2 float2x2 float3 float3x3 float4 "
            + "float4x4 half half2 half3 half4 int int2 int3 int4 ivec2 ivec3 ivec4 mat2 mat3 mat4 sampler2D "
            + "SamplerState shader Texture2D uint vec2 vec3 vec4 void"),
        Words("true false"), capitalizedTypes: false);

    /// <summary>JSON, tolerating comments.</summary>
    public static CLikeSyntax Json { get; } = new([], null, Words("true false null"), quotes: "\"", directive: null,
        capitalizedTypes: false);

    /// <summary>Python, including triple-quoted strings and decorators.</summary>
    public static CLikeSyntax Python { get; } = new(
        Words("and as assert async await break case class continue def del elif else except finally for from "
            + "global if import in is lambda match nonlocal not or pass raise return try while with yield"),
        Words("bool bytes dict float int list object set str tuple"),
        Words("True False None"), lineComment: "#", blockOpen: null, multilineString: "\"\"\"", directive: '@',
        capitalizedTypes: false);

    static string[] Words(string words) => words.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <inheritdoc />
    public int Tokenize(ReadOnlySpan<char> line, int state, List<SyntaxToken> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var i = 0;
        if (state != Code)
        {
            (i, state) = Continue(line, state, tokens);
        }

        if (state == Code && _directive is { } directive && line.TrimStart() is [var first, ..] && first == directive)
        {
            tokens.Add(new SyntaxToken(i, line.Length - i, SyntaxKind.Preprocessor));
            return Code;
        }

        while (i < line.Length && state == Code)
        {
            (i, state) = Next(line, i, tokens);
        }

        return state;
    }

    /// <summary>Finishes a comment or string that started on an earlier line.</summary>
    (int End, int State) Continue(ReadOnlySpan<char> line, int state, List<SyntaxToken> tokens)
    {
        var (close, kind) = state == InBlockComment ? (_blockClose, SyntaxKind.Comment) : (_multilineString,
            SyntaxKind.String);
        var at = close is null ? -1 : line.IndexOf(close, StringComparison.Ordinal);
        var end = at < 0 ? line.Length : at + close!.Length;
        tokens.Add(new SyntaxToken(0, end, kind));
        return (end, at < 0 ? state : Code);
    }

    (int End, int State) Next(ReadOnlySpan<char> line, int i, List<SyntaxToken> tokens)
    {
        var rest = line[i..];
        if (char.IsWhiteSpace(line[i]))
        {
            return (i + 1, Code);
        }

        if (_lineComment is not null && rest.StartsWith(_lineComment, StringComparison.Ordinal))
        {
            tokens.Add(new SyntaxToken(i, line.Length - i, SyntaxKind.Comment));
            return (line.Length, Code);
        }

        if (_blockOpen is not null && rest.StartsWith(_blockOpen, StringComparison.Ordinal))
        {
            return Spanning(line, i, _blockOpen, _blockClose!, SyntaxKind.Comment, InBlockComment, tokens);
        }

        if (_multilineString is not null && rest.StartsWith(_multilineString, StringComparison.Ordinal))
        {
            return Spanning(line, i, _multilineString, _multilineString, SyntaxKind.String, InMultilineString, tokens);
        }

        return (Single(line, i, tokens), Code);
    }

    /// <summary>A comment or string that may close later on this line or continue onto the next.</summary>
    static (int End, int State) Spanning(ReadOnlySpan<char> line, int i, string open, string close, SyntaxKind kind,
        int openState, List<SyntaxToken> tokens)
    {
        var at = line[(i + open.Length)..].IndexOf(close, StringComparison.Ordinal);
        var end = at < 0 ? line.Length : i + open.Length + at + close.Length;
        tokens.Add(new SyntaxToken(i, end - i, kind));
        return (end, at < 0 ? openState : Code);
    }

    /// <summary>A token that ends on this line: string, number, word, operator or punctuation.</summary>
    int Single(ReadOnlySpan<char> line, int i, List<SyntaxToken> tokens)
    {
        var c = line[i];
        var (end, kind) = c switch
        {
            _ when _quotes.Contains(c) => (Quoted(line, i), SyntaxKind.String),
            _ when char.IsAsciiDigit(c) => (Number(line, i), SyntaxKind.Number),
            _ when char.IsLetter(c) || c == '_' => Word(line, i),
            _ when Operators.Contains(c) => (i + 1, SyntaxKind.Operator),
            _ when Punctuation.Contains(c) => (i + 1, SyntaxKind.Punctuation),
            _ => (i + 1, SyntaxKind.Plain)
        };
        if (kind != SyntaxKind.Plain)
        {
            tokens.Add(new SyntaxToken(i, end - i, kind));
        }

        return end;
    }

    static int Quoted(ReadOnlySpan<char> line, int i)
    {
        var quote = line[i];
        var end = i + 1;
        while (end < line.Length && line[end] != quote)
        {
            end += line[end] == '\\' ? 2 : 1;
        }

        return Math.Min(line.Length, end + 1);
    }

    static int Number(ReadOnlySpan<char> line, int i)
    {
        var end = i;
        while (end < line.Length && (char.IsAsciiLetterOrDigit(line[end]) || line[end] is '.' or '_'))
        {
            end++;
        }

        return end;
    }

    (int End, SyntaxKind Kind) Word(ReadOnlySpan<char> line, int i)
    {
        var end = i;
        while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
        {
            end++;
        }

        var word = line[i..end].ToString();
        return (end, KindOf(word, line[end..].TrimStart()));
    }

    SyntaxKind KindOf(string word, ReadOnlySpan<char> after) => word switch
    {
        _ when _keywords.Contains(word) => SyntaxKind.Keyword,
        _ when _types.Contains(word) => SyntaxKind.Type,
        _ when _constants.Contains(word) => SyntaxKind.Constant,
        _ when after is ['(', ..] => SyntaxKind.Function,
        _ when _capitalizedTypes && char.IsUpper(word[0]) => SyntaxKind.Type,
        _ => SyntaxKind.Plain
    };
}

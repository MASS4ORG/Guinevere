namespace Guinevere.Tests;

/// <summary>Line-incremental tokenizing and conversion of highlighted code to rich text.</summary>
public class SyntaxHighlightingTests
{
    static (List<SyntaxToken> Tokens, int State) Tokenize(ISyntaxHighlighter highlighter, string line, int state = 0)
    {
        var tokens = new List<SyntaxToken>();
        var end = highlighter.Tokenize(line, state, tokens);
        return (tokens, end);
    }

    static SyntaxKind KindAt(List<SyntaxToken> tokens, string line, string word)
    {
        var at = line.IndexOf(word, StringComparison.Ordinal);
        return tokens.Single(token => token.Start <= at && at < token.Start + token.Length).Kind;
    }

    [Fact]
    public void CSharpLineYieldsEveryKind()
    {
        const string Line = "public static int Count(string name) => name.Length + 42; // done";
        var (tokens, state) = Tokenize(CLikeSyntax.CSharp, Line);

        Assert.Equal(0, state);
        Assert.Equal(SyntaxKind.Keyword, KindAt(tokens, Line, "public"));
        Assert.Equal(SyntaxKind.Type, KindAt(tokens, Line, "int"));
        Assert.Equal(SyntaxKind.Function, KindAt(tokens, Line, "Count"));
        Assert.Equal(SyntaxKind.Type, KindAt(tokens, Line, "Length"));
        Assert.Equal(SyntaxKind.Number, KindAt(tokens, Line, "42"));
        Assert.Equal(SyntaxKind.Operator, KindAt(tokens, Line, "+"));
        Assert.Equal(SyntaxKind.Punctuation, KindAt(tokens, Line, ";"));
        Assert.Equal(SyntaxKind.Comment, KindAt(tokens, Line, "// done"));
        Assert.DoesNotContain(tokens, token => Line.Substring(token.Start, token.Length) == "name");
    }

    [Fact]
    public void StringsHonourEscapesAndConstantsAreRecognised()
    {
        const string Line = "var s = \"a\\\"b\" + 'c' ?? null;";
        var (tokens, _) = Tokenize(CLikeSyntax.CSharp, Line);

        Assert.Contains(tokens, token => token.Kind == SyntaxKind.String && token.Length == 6);
        Assert.Equal(SyntaxKind.String, KindAt(tokens, Line, "'c'"));
        Assert.Equal(SyntaxKind.Constant, KindAt(tokens, Line, "null"));
    }

    [Fact]
    public void BlockCommentsAndRawStringsCarryAcrossLines()
    {
        var (open, inComment) = Tokenize(CLikeSyntax.CSharp, "x /* start");
        var (middle, stillInComment) = Tokenize(CLikeSyntax.CSharp, "still", inComment);
        var (close, afterComment) = Tokenize(CLikeSyntax.CSharp, "end */ y", stillInComment);
        var (_, inString) = Tokenize(CLikeSyntax.CSharp, "var s = \"\"\"");
        var (closing, afterString) = Tokenize(CLikeSyntax.CSharp, "text\"\"\";", inString);

        Assert.Equal(SyntaxKind.Comment, open[^1].Kind);
        Assert.NotEqual(0, inComment);
        Assert.Equal(new SyntaxToken(0, 5, SyntaxKind.Comment), Assert.Single(middle));
        Assert.Equal(inComment, stillInComment);
        Assert.Equal(new SyntaxToken(0, 6, SyntaxKind.Comment), close[0]);
        Assert.Equal(0, afterComment);
        Assert.NotEqual(inComment, inString);
        Assert.Equal(SyntaxKind.String, closing[0].Kind);
        Assert.Equal(0, afterString);
    }

    [Fact]
    public void SingleLineBlockCommentClosesOnTheSameLine()
    {
        var (tokens, state) = Tokenize(CLikeSyntax.Shader, "float /* c */ x");

        Assert.Equal(0, state);
        Assert.Contains(new SyntaxToken(6, 7, SyntaxKind.Comment), tokens);
    }

    [Fact]
    public void DirectivesTakeTheWholeLine()
    {
        var (tokens, _) = Tokenize(CLikeSyntax.Shader, "  #version 300 es");

        Assert.Equal(new SyntaxToken(0, 17, SyntaxKind.Preprocessor), Assert.Single(tokens));
    }

    [Fact]
    public void PythonUsesHashCommentsDecoratorsAndTripleQuotes()
    {
        var (comment, _) = Tokenize(CLikeSyntax.Python, "x = None  # note");
        var (decorator, _) = Tokenize(CLikeSyntax.Python, "@dataclass");
        var (_, inDoc) = Tokenize(CLikeSyntax.Python, "\"\"\"doc");

        Assert.Equal(SyntaxKind.Comment, comment[^1].Kind);
        Assert.Contains(comment, token => token.Kind == SyntaxKind.Constant);
        Assert.Equal(SyntaxKind.Preprocessor, Assert.Single(decorator).Kind);
        Assert.NotEqual(0, inDoc);
    }

    [Fact]
    public void JsonHasConstantsButNoCapitalizedTypes()
    {
        const string Line = "{\"Key\": true, \"n\": 1.5e3, \"x\": Value}";
        var (tokens, _) = Tokenize(CLikeSyntax.Json, Line);

        Assert.Equal(SyntaxKind.Constant, KindAt(tokens, Line, "true"));
        Assert.Equal(SyntaxKind.Number, KindAt(tokens, Line, "1.5e3"));
        Assert.DoesNotContain(tokens, token => token.Kind == SyntaxKind.Type);
    }

    [Fact]
    public void LanguagesResolveByNameOrAlias()
    {
        var custom = new CLikeSyntax(["fn"]);

        SyntaxHighlighting.Register("rustish", custom);

        Assert.Same(CLikeSyntax.CSharp, SyntaxHighlighting.ForLanguage(" CS "));
        Assert.Same(CLikeSyntax.JavaScript, SyntaxHighlighting.ForLanguage("ts"));
        Assert.Same(CLikeSyntax.Shader, SyntaxHighlighting.ForLanguage("glsl"));
        Assert.Same(custom, SyntaxHighlighting.ForLanguage("rustish"));
        Assert.Null(SyntaxHighlighting.ForLanguage("cobol"));
        Assert.Null(SyntaxHighlighting.ForLanguage(null));
        Assert.Throws<ArgumentException>(() => SyntaxHighlighting.Register(" ", custom));
    }

    [Fact]
    public void ToRichTextStylesTokensWithTheTheme()
    {
        const string Code = "int x = 1;\n/* a\nb */ return x;";
        var text = SyntaxHighlighting.ToRichText(Code, CLikeSyntax.CSharp, SyntaxTheme.Light);
        var ranges = text.Content.Ranges;
        Color? ColorAt(int offset) => ranges.Single(range => range.Start <= offset && offset < range.End).Style.Color;

        Assert.Equal(SyntaxTheme.Light.StyleOf(SyntaxKind.Type)!.Value.Color, ColorAt(0));
        Assert.Equal(SyntaxTheme.Light.StyleOf(SyntaxKind.Comment)!.Value.Color, ColorAt(Code.IndexOf('b')));
        Assert.Equal(SyntaxTheme.Light.StyleOf(SyntaxKind.Keyword)!.Value.Color, ColorAt(Code.IndexOf("return",
            StringComparison.Ordinal)));
        Assert.Null(SyntaxTheme.Light.StyleOf(SyntaxKind.Plain));
        Assert.Single(SyntaxHighlighting.ToRichText(Code, null, SyntaxTheme.Dark).Content.Ranges);
    }
}

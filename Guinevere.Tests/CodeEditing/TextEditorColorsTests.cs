using Guinevere.Tests.Mocks;
using static Guinevere.Tests.CodeEditing.EditorTestText;

namespace Guinevere.Tests.CodeEditing;

/// <summary>The editor's colors come from the <c>editor-*</c> and <c>syntax-*</c> rules of the sheets, once per frame.</summary>
public sealed class TextEditorColorsTests : IDisposable
{
    readonly FrameHarness _harness = new(600, 400);

    public void Dispose() => _harness.Dispose();

    static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    /// <summary>Reads the colors inside a frame, after <paramref name="setup"/> has chosen the sheets.</summary>
    TextEditorColors Read(Action<Gui>? setup = null)
    {
        TextEditorColors colors = default;
        _harness.Frame(gui =>
        {
            setup?.Invoke(gui);
            ExcaliburStyles.Ensure(gui);
            colors = TextEditorColors.Read(gui);
        });
        return colors;
    }

    static TextEditorColors Dark => new(
        Background: Argb(255, 30, 30, 30), Text: Argb(255, 212, 212, 212), Gutter: Argb(255, 30, 30, 30),
        LineNumber: Argb(255, 110, 118, 129), ActiveLineNumber: Argb(255, 200, 200, 200),
        CurrentLine: Argb(255, 40, 40, 44), Selection: Argb(255, 38, 79, 120), Caret: Argb(255, 174, 175, 173),
        FindMatch: Argb(110, 234, 92, 0), BracketMatch: Argb(80, 120, 140, 160), Error: Argb(255, 241, 76, 76),
        Warning: Argb(255, 205, 173, 0), Information: Argb(255, 55, 148, 255), Hint: Argb(160, 160, 160, 160),
        Popup: Argb(255, 37, 37, 38), PopupBorder: Argb(255, 69, 69, 69), PopupSelection: Argb(255, 4, 57, 94),
        Scrollbar: Argb(90, 121, 121, 121), Syntax: Styles(SyntaxTheme.Dark));

    static TextEditorColors Light => Dark with
    {
        Background = Argb(255, 255, 255, 255),
        Text = Argb(255, 30, 30, 30),
        Gutter = Argb(255, 245, 245, 245),
        LineNumber = Argb(255, 160, 160, 160),
        ActiveLineNumber = Argb(255, 30, 30, 30),
        CurrentLine = Argb(255, 240, 244, 250),
        Selection = Argb(255, 173, 214, 255),
        Caret = Argb(255, 0, 0, 0),
        FindMatch = Argb(110, 255, 200, 0),
        BracketMatch = Argb(70, 0, 100, 200),
        Popup = Argb(255, 250, 250, 250),
        PopupBorder = Argb(255, 200, 200, 200),
        PopupSelection = Argb(255, 210, 228, 255),
        Scrollbar = Argb(70, 100, 100, 100),
        Syntax = Styles(SyntaxTheme.Light),
    };

    static TextSpanStyle?[] Styles(SyntaxTheme theme) =>
        [.. Enum.GetValues<SyntaxKind>().Select(theme.StyleOf)];

    static void AssertSame(TextEditorColors expected, TextEditorColors actual)
    {
        Assert.Equal(expected with { Syntax = [] }, actual with { Syntax = [] });
        Assert.Equal(expected.Syntax, actual.Syntax);
    }

    /// <summary>Every part the editor reads is defined by both full base sheets, so none relies on a fallback.</summary>
    [Theory]
    [InlineData("default")]
    [InlineData("classic")]
    public void BothBaseSheetsDefineEveryPart(string name)
    {
        var gui = new Gui();
        ExcaliburStyles.SetBaseSheet(gui, name == "default" ? ExcaliburStyles.DefaultSheet : ExcaliburStyles.Classic);
        string[] backgrounds =
        [
            "editor", "editor-gutter", "editor-current-line", "editor-selection", "editor-find-match",
            "editor-bracket-match", "editor-popup", "editor-popup-selection", "editor-scrollbar",
        ];
        string[] inks =
        [
            "editor", "editor-line-number", "editor-caret", "editor-error", "editor-warning", "editor-information",
            "editor-hint", "syntax-keyword", "syntax-type", "syntax-function", "syntax-string", "syntax-number",
            "syntax-comment", "syntax-preprocessor", "syntax-constant",
        ];

        foreach (var part in backgrounds)
        {
            Assert.True(gui.ResolveStyle(part).GetColor("background-color").HasValue, part);
        }

        foreach (var part in inks)
        {
            Assert.True(gui.ResolveStyle(part).GetColor("color").HasValue, part);
        }

        Assert.True(gui.ResolveStyle("editor-popup").GetColor("border-color").HasValue);
        Assert.True(gui.ResolveStyle("editor-line-number", modifiers: ["current"]).GetColor("color").HasValue);
        Assert.Equal("italic", gui.ResolveStyle("syntax-comment").Get("font-style"));
    }

    /// <summary>The default sheet reproduces the colors the dark editor theme had.</summary>
    [Fact]
    public void TheDefaultSheetGivesTheDarkColors() => AssertSame(Dark, Read());

    /// <summary>The classic base sheet is dark and reproduces the same colors.</summary>
    [Fact]
    public void TheClassicSheetGivesTheDarkColors() =>
        AssertSame(Dark, Read(static gui => ExcaliburStyles.SetBaseSheet(gui, ExcaliburStyles.Classic)));

    /// <summary>The light theme sheets give the colors the light editor theme had, over either base.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheLightThemeSheetsGiveTheLightColors(bool classic, bool mono)
    {
        var colors = Read(gui =>
        {
            ExcaliburStyles.SetBaseSheet(gui, classic ? ExcaliburStyles.Classic : ExcaliburStyles.DefaultSheet);
            ExcaliburStyles.SetTheme(gui, mono ? ExcaliburStyles.MonoLight : ExcaliburStyles.Light);
        });

        AssertSame(Light, colors);
    }

    /// <summary>The dark theme sheet changes nothing: the dark colors are the default sheet's.</summary>
    [Fact]
    public void TheMonoDarkThemeKeepsTheDarkColors() =>
        AssertSame(Dark, Read(static gui => ExcaliburStyles.SetTheme(gui, ExcaliburStyles.MonoDark)));

    /// <summary>Rules and tokens of an application sheet override single colors and nothing else.</summary>
    [Fact]
    public void AnApplicationSheetOverridesWhatItSets()
    {
        var sheet = StyleSheet.Parse("""
            editor-selection { background-color = #123456; }
            editor-line-number:current { color = #abcdef; }
            editor-popup { border-color = #fedcba; }
            syntax-operator { color = #010203; font-style = italic; }
            syntax-keyword { color = #040506; }
            $editor-caret = #ff0000;
            """);
        var colors = Read(gui => gui.StyleSheets.Add(sheet));

        Assert.Equal(Argb(255, 0x12, 0x34, 0x56), colors.Selection);
        Assert.Equal(Argb(255, 0xab, 0xcd, 0xef), colors.ActiveLineNumber);
        Assert.Equal(Argb(255, 0xfe, 0xdc, 0xba), colors.PopupBorder);
        Assert.Equal(Argb(255, 255, 0, 0), colors.Caret);
        Assert.Equal(new TextSpanStyle { Color = Argb(255, 1, 2, 3), Italic = true }, colors.StyleOf(SyntaxKind.Operator));
        Assert.Equal(TextSpanStyle.Colored(Argb(255, 4, 5, 6)), colors.StyleOf(SyntaxKind.Keyword));
        AssertSame(
            Dark with
            {
                Selection = colors.Selection,
                ActiveLineNumber = colors.ActiveLineNumber,
                PopupBorder = colors.PopupBorder,
                Caret = colors.Caret,
                Syntax = colors.Syntax,
            }, colors);
        Assert.Equal(SyntaxTheme.Dark.StyleOf(SyntaxKind.Type), colors.StyleOf(SyntaxKind.Type));
    }

    /// <summary>A GUI with no sheet that defines the parts still gets a legible, dark editor.</summary>
    [Fact]
    public void WithoutAnyRulesTheEditorFallsBackToDark()
    {
        TextEditorColors colors = default;
        _harness.Frame(gui => colors = TextEditorColors.Read(gui));

        AssertSame(Dark, colors);
    }

    /// <summary>Severities map to their squiggle colors, and the styles convert to a syntax theme for Markdown.</summary>
    [Fact]
    public void SeveritiesAndSyntaxConvert()
    {
        var colors = Dark;

        Assert.Equal(colors.Error, colors.ColorOf(DiagnosticSeverity.Error));
        Assert.Equal(colors.Warning, colors.ColorOf(DiagnosticSeverity.Warning));
        Assert.Equal(colors.Information, colors.ColorOf(DiagnosticSeverity.Information));
        Assert.Equal(colors.Hint, colors.ColorOf(DiagnosticSeverity.Hint));
        Assert.Equal(SyntaxTheme.Dark.Styles, colors.ToSyntaxTheme().Styles);
    }

    /// <summary>The sheets are read once per frame, whatever the length of the document or the passes it takes.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(400)]
    public void TheColorsAreReadOncePerFrame(int lines)
    {
        var code = string.Join('\n', Enumerable.Range(0, lines).Select(static i => $"int value{i} = {i}; // c"));
        var state = Editor(code);
        state.Find.IsOpen = true;
        for (var frame = 1; frame <= 3; frame++)
        {
            _harness.Frame(gui => gui.TextEditor(state, new TextEditorOptions { FontSize = 14 }, 600, 400));
            Assert.Equal(frame, state.View.ColorReads);
        }
    }
}

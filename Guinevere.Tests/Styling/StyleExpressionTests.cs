namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyleExpression"/> and <see cref="StyleFunctions"/>: values computed at resolve time.</summary>
public class StyleExpressionTests
{
    static ResolvedStyle Box(string declarations, string tokens = "", IReadOnlyList<StyleVariable>? variables = null) =>
        StyleResolver.Resolve([StyleSheet.Parse($"{tokens}\nbox {{ {declarations} }}")],
            new StyleTarget("box", null, []), variables);

    /// <summary>Arithmetic follows precedence and parentheses.</summary>
    [Fact]
    public void Arithmetic_FollowsPrecedence()
    {
        var style = Box("width = 10 + 4 * 2; height = (10 + 4) * 2; gap = 12 / 4 - 1; margin = 10 - 4;");

        Assert.Equal(("18", "28", "2", "6"),
            (style.Get("width"), style.Get("height"), style.Get("gap"), style.Get("margin")));
    }

    /// <summary><c>em</c> scales with <c>$font-size</c>, defaulting to 16, and works inside <c>calc()</c>.</summary>
    [Fact]
    public void Em_UsesFontSizeToken()
    {
        var themed = Box("width = calc(2em + 4); padding = 0.5em 1em;", "$font-size = 10 * 2;");

        Assert.Equal(("44", "10 20"), (themed.Get("width"), themed.Get("padding")));
        Assert.Equal("24", Box("width = 1.5em;").Get("width"));
    }

    /// <summary>Only evaluated parts are rewritten; other items keep their spelling and units.</summary>
    [Fact]
    public void Lists_RewriteOnlyEvaluatedItems()
    {
        var style = Box("""
            padding = 10px 16px; margin = 10 -4; shadow = 0 2px rgba(0, 0, 0, 51);
            width = ratio(2); src = url("http://example.com/a.png"); height = calc(100% - 10px);
            family = "Inter", sans-serif; columns = 2 * 3 1fr !important; offset = -2px + -1 -$x;
            """);

        Assert.Equal("10px 16px", style.Get("padding"));
        Assert.Equal("10 -4", style.Get("margin"));
        Assert.Equal("0 2px #00000033", style.Get("shadow"));
        Assert.Equal("ratio(2)", style.Get("width"));
        Assert.Equal("url(\"http://example.com/a.png\")", style.Get("src"));
        Assert.Equal("calc(100% - 10px)", style.Get("height"));
        Assert.Equal("\"Inter\", sans-serif", style.Get("family"));
        Assert.Equal("6 1fr !important", style.Get("columns"));
        Assert.Equal("-3 -$x", style.Get("offset"));
    }

    /// <summary>Color constructors and functions produce canonical <c>#rrggbbaa</c> values.</summary>
    [Theory]
    [InlineData("hsl(0, 100%, 50%)", "#ff0000ff")]
    [InlineData("hsla(120, 1, 0.25, 50%)", "#00800080")]
    [InlineData("rgb(255, 0, 0)", "#ff0000ff")]
    [InlineData("rgba(0, 0, 255, 128)", "#0000ff80")]
    [InlineData("rgb(100%, 0%, 0%, 255)", "#ff0000ff")]
    [InlineData("rgba(1, 1, 1, 1)", "#01010101")]
    [InlineData("rgba1(1, 1, 1, 1)", "#ffffffff")]
    [InlineData("rgb1(0, 0, 1)", "#0000ffff")]
    [InlineData("rgba1(0.2, 0.2, 0.2, 50%)", "#33333380")]
    [InlineData("mix(#000000, #ffffff, 0.5)", "#808080ff")]
    [InlineData("alpha(red, 50%)", "#ff000080")]
    [InlineData("lighten(#000000, 0.25)", "#404040ff")]
    [InlineData("darken(#ffffff, 25%)", "#bfbfbfff")]
    [InlineData("contrast-ink(#202020)", "#ffffffff")]
    [InlineData("contrast-ink(#f0f0f0)", "#000000ff")]
    [InlineData("contrast-ink(#808080, #111111, #eeeeee)", "#111111ff")]
    [InlineData("mix(hsl(0, 100%, 50%), blue, 0)", "#ff0000ff")]
    [InlineData("hsl(60, 100%, 50%)", "#ffff00ff")]
    [InlineData("hsl(180deg, 100%, 50%)", "#00ffffff")]
    [InlineData("hsl(240, 100%, 50%)", "#0000ffff")]
    [InlineData("hsl(300, 100%, 50%)", "#ff00ffff")]
    [InlineData("hsl(-60, 100%, 50%)", "#ff00ffff")]
    [InlineData("shade(#00ff00, 0)", "#00ff00ff")]
    [InlineData("shade(#0000ff, 0)", "#0000ffff")]
    [InlineData("shade(#ff00ff, 0)", "#ff00ffff")]
    public void ColorFunctions_Evaluate(string expression, string expected) =>
        Assert.Equal(expected, Box($"color = {expression};").Get("color"));

    /// <summary><c>min</c>, <c>max</c> and <c>clamp</c> pick numbers of one unit.</summary>
    [Theory]
    [InlineData("min(4, 2, 8)", "2")]
    [InlineData("max(4, 2, 8)", "8")]
    [InlineData("clamp(0, 12, 10)", "10")]
    [InlineData("clamp(0, -3, 10)", "0")]
    [InlineData("max(10%, 20%) * 2", "40%")]
    [InlineData("min(10%, 20)", "min(10%, 20)")]
    [InlineData("50% / 2", "25%")]
    [InlineData("10% * 10%", "10% * 10%")]
    [InlineData("10 / 50%", "10 / 50%")]
    [InlineData("-(2 + 3)", "-5")]
    public void NumericFunctions_Evaluate(string expression, string expected) =>
        Assert.Equal(expected, Box($"value = {expression};").Get("value"));

    /// <summary><c>shade</c> follows Godot's ladder: darker for positive offsets, brighter for negative ones.</summary>
    [Fact]
    public void Shade_FollowsContrastLadder()
    {
        var style = Box("""
            dark = shade(#808080, 1); bright = shade(#808080, -1, 1, 0.2); faded = shade(#ff0000, 0, 0.5);
            """, "$contrast = 0.5;");

        Assert.Equal(("#404040ff", "#9a9a9aff", "#ff8080ff"),
            (style.Get("dark"), style.Get("bright"), style.Get("faded")));
        Assert.Equal("#5a5a5aff", Box("dark = shade(#808080, 1);").Get("dark"));
    }

    /// <summary>Derived tokens are readable by the host and follow host overrides.</summary>
    [Fact]
    public void Tokens_DeriveAndFollowHostOverrides()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse("""
            $base = #808080; $contrast = 0.5; $surface = shade($base, 1); $radius = 2 * 3;
            button { bg-color = $surface; }
            """));

        Assert.Equal("#404040ff", gui.StyleSheets.GetToken("$surface"));
        Assert.Equal(gui.StyleSheets.GetToken("surface"), gui.ResolveStyle("button").Get("bg-color"));
        Assert.Equal(6f, gui.StyleSheets.GetTokenLength("radius"));
        Assert.Null(gui.StyleSheets.GetToken("missing"));
        Assert.Null(gui.StyleSheets.GetTokenColor("radius"));

        gui.StyleSheets.SetToken("base", "#ffffff");

        Assert.Equal(Color.FromArgb(255, 128, 128, 128), gui.StyleSheets.GetTokenColor("surface"));
    }

    /// <summary>Typed call-site values are formatted as <c>.pss</c> text before expressions run.</summary>
    [Fact]
    public void TypedVariables_FeedExpressions()
    {
        var style = Box("bg-color = mix($tint, #ffffff, 50%); width = $size * 2; on = $flag;", variables:
        [
            new StyleVariable("tint", Color.FromArgb(255, 0, 0, 0)),
            new StyleVariable("size", 12.5f),
            new StyleVariable("flag", true),
        ]);

        Assert.Equal(("#808080ff", "25", "true"), (style.Get("bg-color"), style.Get("width"), style.Get("on")));
    }

    /// <summary>Values that only fail once tokens are known fall back to their text, like any unparseable value.</summary>
    [Fact]
    public void RuntimeTypeError_KeepsValue()
    {
        var style = Box("bg-color = mix($c, red, 0.5); size = $c / 0; scale = outer - inner;", "$c = 10;");

        Assert.Equal("mix(10, red, 0.5)", style.Get("bg-color"));
        Assert.Null(style.GetColor("bg-color"));
        Assert.Equal("10 / 0", style.Get("size"));
        Assert.Equal("outer - inner", style.Get("scale"));
    }

    /// <summary>Malformed expressions fail at parse time with the position inside the value.</summary>
    [Theory]
    [InlineData("width = mix(#fff, red);", "mix() expects 3 arguments", 11)]
    [InlineData("width = mix(10, red, 0.5);", "Expected a color", 11)]
    [InlineData("width = 1 +;", "Unexpected end of value", 14)]
    [InlineData("width = 2 / 0;", "Division by zero", 13)]
    [InlineData("width = (1) 2);", "Unexpected ')'", 16)]
    [InlineData("width = contrast-ink(red, blue);", "contrast-ink() expects 1 or 3 arguments", 11)]
    [InlineData("width = min();", "min() expects at least 1 arguments", 11)]
    [InlineData("width = shade(red, 1, 2, 3, 4);", "shade() expects 2 to 4 arguments", 11)]
    [InlineData("width = (,);", "Expected a value", 12)]
    public void SyntaxError_ReportsPosition(string declaration, string reason, int column)
    {
        var error = Assert.Throws<StyleSheetException>(() =>
            StyleSheet.Parse($"box {{\n  {declaration}\n}}", new StyleSheetOptions { SourceName = "theme.pss" }));

        Assert.Contains(reason, error.Reason);
        Assert.Equal(("theme.pss", 2, column), (error.SourceName, error.Line, error.Column));
    }

    /// <summary>An unclosed group inside a value reports the opening parenthesis.</summary>
    [Fact]
    public void Validate_ReportsUnclosedGroup()
    {
        var error = Assert.Throws<StyleExpressionException>(() => StyleExpression.Validate("2 * (1 + 2"));

        Assert.Equal(("Missing ')'", 4), (error.Message, error.Position));
    }

    /// <summary>Token declarations are validated too; a value changed by <c>@const</c> reports its start.</summary>
    [Fact]
    public void SyntaxError_InTokenAndConstValues()
    {
        var token = Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("$x = 1 * ;"));
        var constant = Assert.Throws<StyleSheetException>(() =>
            StyleSheet.Parse("@const n = 2;\nbox { width = @n * ; }"));

        Assert.Equal((1, 9), (token.Line, token.Column));
        Assert.Equal((2, 15), (constant.Line, constant.Column));
    }

    /// <summary>CSS-flavored sheets keep working: <c>var()</c> and <c>--name</c> are opaque until expanded.</summary>
    [Fact]
    public void CssSyntax_EvaluatesAfterExpansion()
    {
        var sheet = StyleSheet.Parse("--a: 3; box { width: calc(var(--a) * 2); }",
            new StyleSheetOptions { AllowCssSyntax = true });

        Assert.Equal("6", StyleResolver.Resolve([sheet], new StyleTarget("box", null, [])).Get("width"));
    }

    /// <summary>Evaluated styles are cached, so hits stay allocation-free.</summary>
    [Fact]
    public void CachedHit_WithExpressions_AllocatesNothing()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse("box { width = 2 * 3; bg-color = mix(red, blue, 0.5); }"));
        var first = gui.ResolveStyle("box");

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) GC.KeepAlive(gui.ResolveStyle("box"));

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(("6", "#800080ff"), (first.Get("width"), first.Get("bg-color")));
    }

    /// <summary><see cref="StyleValue.Format"/> writes typed values in invariant <c>.pss</c> form.</summary>
    [Fact]
    public void Format_WritesTypedValues()
    {
        Assert.Equal("", StyleValue.Format(null));
        Assert.Equal("text", StyleValue.Format("text"));
        Assert.Equal("#ff000080", StyleValue.Format(Color.FromArgb(128, 255, 0, 0)));
        Assert.Equal("#0000ffff", StyleValue.Format(System.Drawing.Color.Blue));
        Assert.Equal("false", StyleValue.Format(false));
        Assert.Equal("(1.5, 2)", StyleValue.Format(new Vector2(1.5f, 2f)));
        Assert.Equal("0.25", StyleValue.Format(0.25d));
        Assert.Equal("Thing", StyleValue.Format(new Thing()));
    }

    sealed class Thing
    {
        public override string ToString() => "Thing";
    }
}

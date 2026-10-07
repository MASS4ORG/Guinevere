namespace Guinevere.Tests.Styling;

/// <summary>Tests for the <c>.pss</c> loader in <see cref="StyleSheet.Parse(string, StyleSheetOptions?)"/>.</summary>
public class StyleSheetPssTests
{
    static readonly StyleTarget Box = new("box", null, []);

    /// <summary>Comment markers inside strings and <c>url()</c> are part of the value.</summary>
    [Fact]
    public void CommentMarkers_InsideStringsAndUrls_Survive()
    {
        var sheet = StyleSheet.Parse("""
            box {
                text-content = "a // b /* c */"; // trailing comment
                image = url("https://example.com/a.png");
                icon = url(http://example.com/b.png); /* block */
            }
            """);

        var style = StyleResolver.Resolve([sheet], Box);
        Assert.Equal("\"a // b /* c */\"", style.Get("text-content"));
        Assert.Equal("url(\"https://example.com/a.png\")", style.Get("image"));
        Assert.Equal("url(http://example.com/b.png)", style.Get("icon"));
        Assert.Equal(new Uri("https://example.com/a.png"), style.GetUrl("image"));
        Assert.Equal(new Uri("http://example.com/b.png"), style.GetUrl("icon"));
    }

    /// <summary><c>@const</c> values are readable by the host and substituted into declarations.</summary>
    [Fact]
    public void Constants_AreExposedAndSubstituted()
    {
        var sheet = StyleSheet.Parse("""
            @const theme-name = "Night";
            @const spacing = 12;
            @const gutter = @spacing;
            box { padding = @spacing; margin = @gutter; color = @unknown; }
            """);

        Assert.Equal("\"Night\"", sheet.Constants["theme-name"]);
        Assert.Equal("12", sheet.Constants["gutter"]);
        var style = StyleResolver.Resolve([sheet], Box);
        Assert.Equal("12", style.Get("padding"));
        Assert.Equal("12", style.Get("margin"));
        Assert.Equal("@unknown", style.Get("color"));
    }

    /// <summary>Errors report <c>source:line:column</c> of the original text after comments and constants.</summary>
    [Fact]
    public void Errors_ReportSourceLineAndColumn()
    {
        const string text = "/* header\n   comment */\n@const pad = 4;\n// note\nbox {\n    padding = @pad;\n    oops;\n}\n";

        var error = Assert.Throws<StyleSheetException>(() =>
            StyleSheet.Parse(text, new StyleSheetOptions { SourceName = "theme.pss" }));

        Assert.Equal(("theme.pss", 7, 5), (error.SourceName, error.Line, error.Column));
        Assert.StartsWith("theme.pss:7:5: Malformed declaration 'oops'", error.Message);
    }

    /// <summary>Selector failures carry the rule position.</summary>
    [Fact]
    public void SelectorErrors_ReportRulePosition()
    {
        var error = Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("\n  Panel >> Button { width = 1; }"));

        Assert.Equal((2, 3), (error.Line, error.Column));
        Assert.Null(error.SourceName);
        Assert.StartsWith("<inline>:2:3:", error.Message);
    }

    /// <summary>The CSS-flavored forms need the migration flag.</summary>
    [Theory]
    [InlineData("box { color: red; }", "AllowCssSyntax")]
    [InlineData("--accent: red;", "AllowCssSyntax")]
    [InlineData("box { --accent = red; }", "AllowCssSyntax")]
    [InlineData("$accent = red; box { color = var(--accent); }", "AllowCssSyntax")]
    public void CssSyntax_IsRejectedWithoutFlag(string text, string hint)
    {
        var error = Assert.Throws<StyleSheetException>(() => StyleSheet.Parse(text));
        Assert.Contains(hint, error.Message, StringComparison.Ordinal);
    }

    /// <summary>The migration flag accepts colon declarations, custom properties and <c>var()</c>.</summary>
    [Fact]
    public void CssSyntax_IsAcceptedWithFlag()
    {
        var sheet = StyleSheet.Parse("--accent: red; box { color: var(--accent); --pad: 3; padding: var(--pad); }",
            new StyleSheetOptions { AllowCssSyntax = true });

        var style = StyleResolver.Resolve([sheet], Box);
        Assert.Equal(("red", "3"), (style.Get("color"), style.Get("padding")));
    }

    /// <summary><c>@font-face</c> and <c>url()</c> resolve relative to a file source.</summary>
    [Fact]
    public void FontFaceAndUrls_ResolveRelativeToFileSource()
    {
        var directory = Directory.CreateTempSubdirectory("pss-");
        try
        {
            var path = Path.Combine(directory.FullName, "theme.pss");
            File.WriteAllText(path, """
                @font-face { font-family = "Inter"; src = url("fonts/Inter.ttf"); }
                box { image = url(images/bg.png); remote = url("https://example.com/x.png"); }
                """);

            var sheet = StyleSheetSource.FromFile(path).Current;

            var face = Assert.Single(sheet.FontFaces);
            Assert.Equal("Inter", face.Family);
            Assert.Equal(new Uri(Path.Combine(directory.FullName, "fonts", "Inter.ttf")), face.Source);
            var style = StyleResolver.Resolve([sheet], Box);
            Assert.Equal(new Uri(Path.Combine(directory.FullName, "images", "bg.png")), style.GetUrl("image"));
            Assert.Equal(new Uri("https://example.com/x.png"), style.GetUrl("remote"));
            Assert.Null(style.GetUrl("missing"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Without a base, relative URLs stay relative; malformed font faces fail.</summary>
    [Fact]
    public void FontFace_WithoutBase_StaysRelative_AndRequiresFamilyAndSource()
    {
        var sheet = StyleSheet.Parse("@font-face { font-family = Mono; src = 'fonts/mono.ttf'; }");

        Assert.Equal(new Uri("fonts/mono.ttf", UriKind.Relative), sheet.FontFaces[0].Source);
        Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("@font-face { src = url(a.ttf); }"));
        Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("@font-face font-family = x;"));
        Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("@font-face { font-family = x; src = url(a.ttf)"));
    }

    /// <summary>Imported rules, tokens and constants come first; the importing sheet overrides them.</summary>
    [Fact]
    public void Import_LayersImportedSheetBelowImporter()
    {
        var sheets = new Dictionary<string, string>
        {
            ["base"] = "@const radius = 4; $accent = blue; box { color = $accent; padding = 1; }",
        };
        var options = new StyleSheetOptions
        {
            SourceName = "app.pss",
            ImportResolver = id => sheets.TryGetValue(id, out var text) ? new StyleSheetText(text) : null,
        };

        var sheet = StyleSheet.Parse("""
            @import "base";
            $accent = red;
            box { padding = 2; border-radius = @radius; }
            """, options);

        var style = StyleResolver.Resolve([sheet], Box);
        Assert.Equal(("red", "2", "4"), (style.Get("color"), style.Get("padding"), style.Get("border-radius")));
        Assert.Equal(2, sheet.Rules.Count);
        Assert.True(sheet.Rules[0].Order < sheet.Rules[1].Order);
    }

    /// <summary>Imports fail clearly without a resolver, for unknown ids, and for cycles.</summary>
    [Fact]
    public void Import_Failures_AreReported()
    {
        var noResolver = Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("@import \"base\";"));
        Assert.Contains("ImportResolver", noResolver.Message, StringComparison.Ordinal);

        var unknown = new StyleSheetOptions { ImportResolver = _ => null };
        Assert.Contains("Cannot resolve", Assert.Throws<StyleSheetException>(() =>
            StyleSheet.Parse("@import 'missing';", unknown)).Message, StringComparison.Ordinal);

        var cyclic = new StyleSheetOptions
        {
            SourceName = "a",
            ImportResolver = id => new StyleSheetText(id == "a" ? "@import \"b\";" : "@import \"a\";"),
        };
        var cycle = Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("@import \"b\";", cyclic));
        Assert.Contains("Cyclic @import", cycle.Message, StringComparison.Ordinal);
        Assert.Equal("b", cycle.SourceName);
    }

    /// <summary>PanGui shapes, effects, mixins and macro calls are kept verbatim and not applied.</summary>
    [Fact]
    public void PanGuiConstructs_AreDeferred()
    {
        var sheet = StyleSheet.Parse("""
            @mixin glow() { effect bg-shape { solidColor(#fff); } }
            @shape pill = roundedRect($width, $height, 4);
            @effect lift(@level) { outerShadow(#000, @level, 0); }
            @style icon(@name) = @name { text-size = 16; }
            @icon(save);
            button {
                width = 10;
                bg-shape = roundedRect($width, $height, 8);
                shape knob = circle(
                    4
                );
                effect knob { solidColor(white); stroke(#999, 1, 0); }
                @glow();
            }
            """);

        Assert.Equal(
            new[]
            {
                StyleDeferredKind.Mixin, StyleDeferredKind.ShapeMacro, StyleDeferredKind.EffectMacro,
                StyleDeferredKind.StyleMacro, StyleDeferredKind.Invocation, StyleDeferredKind.BackgroundShape,
                StyleDeferredKind.Shape, StyleDeferredKind.Effect, StyleDeferredKind.Invocation,
            },
            sheet.Deferred.Select(d => d.Kind));
        Assert.Equal((8, 5), (sheet.Deferred[5].Line, sheet.Deferred[5].Column));
        Assert.StartsWith("shape knob = circle(", sheet.Deferred[6].Text);
        var style = StyleResolver.Resolve([sheet], new StyleTarget("button", null, []));
        Assert.Equal(new[] { "width" }, style.Declarations.Keys);
    }

    /// <summary>Malformed at-rules and statements report errors instead of being skipped.</summary>
    [Theory]
    [InlineData("@const = 1;")]
    [InlineData("@import base;")]
    [InlineData("@ ;")]
    [InlineData("@mixin broken() { a = 1;")]
    [InlineData("@shape pill = circle(1)")]
    [InlineData("$token = 1 { }")]
    [InlineData("box")]
    [InlineData(" { width = 1; }")]
    [InlineData("box { width = 1")]
    public void MalformedStatements_Throw(string text) =>
        Assert.Throws<StyleSheetException>(() => StyleSheet.Parse(text));

    /// <summary>
    /// <c>#inherit-properties</c> copies the parent's own style only; <c>#inherit-selector</c> joins parent
    /// relationships only.
    /// </summary>
    [Fact]
    public void InheritVariants_SplitPropertiesFromSelectors()
    {
        var sheet = StyleSheet.Parse("""
            box { padding = 20; }
            sidebar > box { margin = 5; }
            box > button { width = 80; }
            looks-like-box #inherit-properties(box) { color = red; }
            acts-like-box #inherit-selector(box) { color = blue; }
            """);
        var sidebar = new StyleTarget("sidebar", null, []);

        var looks = StyleResolver.Resolve([sheet], new StyleTarget("looks-like-box", null, [], Ancestors: [sidebar]));
        Assert.Equal<(string?, string?)>(("20", null), (looks.Get("padding"), looks.Get("margin")));
        var acts = StyleResolver.Resolve([sheet], new StyleTarget("acts-like-box", null, [], Ancestors: [sidebar]));
        Assert.Equal<(string?, string?)>((null, "5"), (acts.Get("padding"), acts.Get("margin")));

        StyleTarget ButtonIn(string type) => new("button", null, [], Ancestors: [new StyleTarget(type, null, [])]);
        Assert.Null(StyleResolver.Resolve([sheet], ButtonIn("looks-like-box")).Get("width"));
        Assert.Equal("80", StyleResolver.Resolve([sheet], ButtonIn("acts-like-box")).Get("width"));
        Assert.Equal(new[] { "box" }, sheet.PropertyInheritance["looks-like-box"]);
        Assert.Equal(new[] { "box" }, sheet.SelectorInheritance["acts-like-box"]);
    }

    /// <summary>Cycles across inheritance kinds are rejected at the declaring rule.</summary>
    [Fact]
    public void InheritVariants_RejectCycles()
    {
        var error = Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("""
            a #inherit-properties(b) { width = 1; }
            b #inherit-selector(a) { width = 2; }
            """));
        Assert.Contains("Cyclic style inheritance", error.Message, StringComparison.Ordinal);
    }
}

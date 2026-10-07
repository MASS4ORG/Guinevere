namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyleResolver"/> — the <c>.uss</c> cascade.</summary>
public class StyleResolverTests
{
    /// <summary>Immediate-mode values override stylesheet variables using invariant formatting.</summary>
    [Fact]
    public void ScopedVariables_OverrideStyleVariables()
    {
        var sheet = StyleSheet.Parse("$progress = 0; progress { width = $progress; }");
        var style = StyleResolver.Resolve([sheet], new StyleTarget("progress", null, []),
            [new StyleVariable("progress", 0.65f)]);

        Assert.Equal("0.65", style.Get("width"));
    }

    /// <summary>Inherited tags copy parent declarations and match parent hierarchy selectors.</summary>
    [Fact]
    public void Inheritance_CopiesPropertiesAndSelectorIdentity()
    {
        var sheet = StyleSheet.Parse("""
            box { padding = 20; color = white; }
            warning-box #inherit(box) { color = orange; }
            box > button { width = 80; }
            """);

        var warning = StyleResolver.Resolve([sheet], new StyleTarget("warning-box", null, []));
        Assert.Equal("20", warning.Get("padding"));
        Assert.Equal("orange", warning.Get("color"));

        var button = new StyleTarget("button", null, [], Ancestors: [new StyleTarget("warning-box", null, [])]);
        Assert.Equal("80", StyleResolver.Resolve([sheet], button).Get("width"));
    }

    /// <summary>Inheritance cycles fail during parsing.</summary>
    [Fact]
    public void Inheritance_RejectsCycles() => Assert.Throws<StyleSheetException>(() => StyleSheet.Parse("""
        a #inherit(b) { width = 1; }
        b #inherit(a) { width = 2; }
        """));
    static ResolvedStyle Resolve(string css, StyleTarget target) =>
        StyleResolver.Resolve([StyleSheet.Parse(css)], target);

    /// <summary>A more specific selector overrides a less specific one regardless of order.</summary>
    [Fact]
    public void Specificity_IdBeatsClassBeatsType()
    {
        var style = Resolve("""
            #save { color = #ff0000; }
            .btn  { color = #00ff00; }
            Button { color = #0000ff; }
            """, new StyleTarget("Button", "save", ["btn"]));

        Assert.Equal("#ff0000", style.Get("color"));
    }

    /// <summary>Equal specificity falls back to source order (last wins).</summary>
    [Fact]
    public void SourceOrder_BreaksTies()
    {
        var style = Resolve("""
            .btn { color = #111111; }
            .btn { color = #222222; }
            """, new StyleTarget("Button", null, ["btn"]));

        Assert.Equal("#222222", style.Get("color"));
    }

    /// <summary>A <c>:hover</c> rule only applies when the target is hovered.</summary>
    [Fact]
    public void Modifier_AppliesOnlyInState()
    {
        const string css = """
            .btn        { background-color = #101010; }
            .btn:hover  { background-color = #303030; }
            """;
        var target = new StyleTarget("Button", null, ["btn"]);

        Assert.Equal("#101010", Resolve(css, target).Get("background-color"));
        Assert.Equal("#303030", Resolve(css, target with { State = StyleState.Hover }).Get("background-color"));
    }

    /// <summary>Declarations merge across matching rules; the typed accessors parse them.</summary>
    [Fact]
    public void Merges_Declarations_And_Expands_Variables()
    {
        var style = Resolve("""
            $pad = 12;
            .card { background-color = #202020; border-radius = 8; }
            .card { padding = $pad; }
            """, new StyleTarget("VisualElement", null, ["card"]));

        Assert.Equal(new Color?(Color.FromArgb(255, 32, 32, 32)).Value.R, style.GetColor("background-color")!.Value.R);
        Assert.Equal(8f, style.GetLength("border-radius"));
        Assert.Equal("12", style.Get("padding"));
    }

    /// <summary>A later sheet's top-level token overrides an earlier sheet's token in that sheet's rules.</summary>
    [Fact]
    public void LaterSheetToken_OverridesEarlierSheet()
    {
        var theme = StyleSheet.Parse("$accent = red; box { color = $accent; }");
        var overrides = StyleSheet.Parse("$accent = blue;");

        Assert.Equal("blue", StyleResolver.Resolve([theme, overrides], new StyleTarget("box", null, [])).Get("color"));
        Assert.Equal("red", StyleResolver.Resolve([overrides, theme], new StyleTarget("box", null, [])).Get("color"));
    }

    /// <summary>A host token wins over every sheet's token.</summary>
    [Fact]
    public void HostToken_WinsOverSheets()
    {
        var sheets = new StyleSheetCollection
        {
            StyleSheet.Parse("$accent = red; box { color = $accent; }"),
            StyleSheet.Parse("$accent = blue;"),
        };
        sheets.SetToken("accent", "green");

        Assert.Equal("green", StyleResolver.Resolve(sheets, new StyleTarget("box", null, [])).Get("color"));
    }

    /// <summary>A rule-scoped token set by a more specific rule applies to the base rule's declarations.</summary>
    [Fact]
    public void RuleScopedToken_FromModifierRule_AppliesToBaseDeclarations()
    {
        var sheet = StyleSheet.Parse("""
            $size = 1;
            toggle {
                $offset = 0;
                offset = $offset;
                size = $size;
                :enabled { $offset = 1; }
            }
            """);

        var enabled = StyleResolver.Resolve([sheet], new StyleTarget("toggle", null, [], Modifiers: ["enabled"]));
        Assert.Equal(("1", "1"), (enabled.Get("offset"), enabled.Get("size")));
        Assert.Equal("0", StyleResolver.Resolve([sheet], new StyleTarget("toggle", null, [])).Get("offset"));
        Assert.False(enabled.Has("--offset"));
    }

    /// <summary>Tokens referencing tokens expand transitively; self-references stop at the depth limit.</summary>
    [Fact]
    public void Tokens_ExpandTransitively()
    {
        var sheet = StyleSheet.Parse("$base = 4; $double = $base $base; $loop = $loop; box { gap = $double; x = $loop; }");
        var style = StyleResolver.Resolve([sheet], new StyleTarget("box", null, []));

        Assert.Equal("4 4", style.Get("gap"));
        Assert.Equal("$loop", style.Get("x"));
        Assert.Equal("4 4", sheet.ExpandVariables("$double"));
        Assert.Equal("9", sheet.ExpandVariables("$base", new Dictionary<string, string> { ["--base"] = "9" }));
    }

    /// <summary>Nothing matches → the empty style.</summary>
    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var style = Resolve(".x { color = red; }", new StyleTarget("Button", null, ["y"]));
        Assert.False(style.Has("color"));
    }
}

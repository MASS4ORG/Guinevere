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
    public void Inheritance_RejectsCycles() => Assert.Throws<FormatException>(() => StyleSheet.Parse("""
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
            #save { color: #ff0000; }
            .btn  { color: #00ff00; }
            Button { color: #0000ff; }
            """, new StyleTarget("Button", "save", ["btn"]));

        Assert.Equal("#ff0000", style.Get("color"));
    }

    /// <summary>Equal specificity falls back to source order (last wins).</summary>
    [Fact]
    public void SourceOrder_BreaksTies()
    {
        var style = Resolve("""
            .btn { color: #111111; }
            .btn { color: #222222; }
            """, new StyleTarget("Button", null, ["btn"]));

        Assert.Equal("#222222", style.Get("color"));
    }

    /// <summary>A <c>:hover</c> rule only applies when the target is hovered.</summary>
    [Fact]
    public void Modifier_AppliesOnlyInState()
    {
        const string css = """
            .btn        { background-color: #101010; }
            .btn:hover  { background-color: #303030; }
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
            --pad: 12;
            .card { background-color: #202020; border-radius: 8; }
            .card { padding: var(--pad); }
            """, new StyleTarget("VisualElement", null, ["card"]));

        Assert.Equal(new Color?(Color.FromArgb(255, 32, 32, 32)).Value.R, style.GetColor("background-color")!.Value.R);
        Assert.Equal(8f, style.GetLength("border-radius"));
        Assert.Equal("12", style.Get("padding"));
    }

    /// <summary>Nothing matches → the empty style.</summary>
    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var style = Resolve(".x { color: red; }", new StyleTarget("Button", null, ["y"]));
        Assert.False(style.Has("color"));
    }
}

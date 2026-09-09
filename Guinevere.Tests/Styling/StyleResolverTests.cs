namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyleResolver"/> — the <c>.uss</c> cascade.</summary>
public class StyleResolverTests
{
    private static ResolvedStyle Resolve(string css, StyleTarget target) =>
        StyleResolver.Resolve([StyleSheet.Parse(css)], target);

    /// <summary>A more specific selector overrides a less specific one regardless of order.</summary>
    [Fact]
    public void Specificity_IdBeatsClassBeatsType()
    {
        var style = Resolve("""
            #save { color: #ff0000; }
            .btn  { color: #00ff00; }
            Button { color: #0000ff; }
            """, new StyleTarget("Button", "save", new[] { "btn" }));

        Assert.Equal("#ff0000", style.Get("color"));
    }

    /// <summary>Equal specificity falls back to source order (last wins).</summary>
    [Fact]
    public void SourceOrder_BreaksTies()
    {
        var style = Resolve("""
            .btn { color: #111111; }
            .btn { color: #222222; }
            """, new StyleTarget("Button", null, new[] { "btn" }));

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
        var target = new StyleTarget("Button", null, new[] { "btn" });

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
            """, new StyleTarget("VisualElement", null, new[] { "card" }));

        Assert.Equal(new Color?(Color.FromArgb(255, 32, 32, 32)).Value.R, style.GetColor("background-color")!.Value.R);
        Assert.Equal(8f, style.GetLength("border-radius"));
        Assert.Equal("12", style.Get("padding"));
    }

    /// <summary>Nothing matches → the empty style.</summary>
    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var style = Resolve(".x { color: red; }", new StyleTarget("Button", null, new[] { "y" }));
        Assert.False(style.Has("color"));
    }
}

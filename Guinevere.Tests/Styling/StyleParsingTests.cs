namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyleValue"/>, <see cref="Selector"/> and <see cref="StyleSheet.Parse"/>.</summary>
public class StyleParsingTests
{
    /// <summary>Descendant and direct-child combinators use nearest-first ancestry.</summary>
    [Fact]
    public void HierarchyCombinators_MatchAncestors()
    {
        var ancestors = new[]
        {
            new StyleTarget("Row", null, ["item"]),
            new StyleTarget("Panel", "settings", ["dialog"]),
        };
        var target = new StyleTarget("Button", null, ["primary"], Ancestors: ancestors);

        Assert.True(Selector.Parse("Panel Button.primary").Matches(target));
        Assert.True(Selector.Parse("Panel > Row > Button.primary").Matches(target));
        Assert.True(Selector.Parse("#settings .primary").Matches(target));
        Assert.False(Selector.Parse("Panel > Button.primary").Matches(target));
    }

    /// <summary>Unknown pseudo-classes are application-defined semantic modifiers.</summary>
    [Fact]
    public void CustomModifiers_CanBeCombinedWithBuiltInState()
    {
        var selector = Selector.Parse("Toggle:checked:hover");
        var target = new StyleTarget("Toggle", null, [], StyleState.Hover, ["checked"]);

        Assert.True(selector.Matches(target));
        Assert.False(selector.Matches(target with { Modifiers = [] }));
        Assert.False(selector.Matches(target with { State = StyleState.None }));
    }
    /// <summary>
    /// Verifies that length values are parsed correctly from plain numbers, pixel, and percentage strings.
    /// </summary>
    /// <param name="text">The input string to parse.</param>
    /// <param name="expected">The expected numeric value.</param>
    /// <param name="percent">Whether the value is a percentage.</param>
    [Theory]
    [InlineData("12", 12f, false)]
    [InlineData("12px", 12f, false)]
    [InlineData("50%", 0.5f, true)]
    public void Value_Length(string text, float expected, bool percent)
    {
        Assert.True(StyleValue.TryLength(text, out var v, out var p));
        Assert.Equal(expected, v, 3);
        Assert.Equal(percent, p);
    }

    /// <summary>
    /// Verifies that hex and rgba color strings are parsed correctly.
    /// </summary>
    [Fact]
    public void Value_Color_Hex_And_Rgb()
    {
        Assert.True(StyleValue.TryColor("#4a90e2", out var hex));
        Assert.Equal((74, 144, 226, 255), (hex.R, hex.G, hex.B, hex.A));

        Assert.True(StyleValue.TryColor("rgba(10,20,30,0.5)", out var rgba));
        Assert.Equal(10, rgba.R);
        Assert.InRange(rgba.A, 120, 132);
    }

    /// <summary>
    /// Verifies that compound selectors with type, classes, and pseudo-states match correctly.
    /// </summary>
    [Fact]
    public void Selector_Parse_Compound()
    {
        var s = Selector.Parse("Button.primary.big:hover");

        Assert.True(s.Matches(new StyleTarget("Button", null, ["primary", "big", "x"], StyleState.Hover)));
        Assert.False(s.Matches(new StyleTarget("Button", null, ["primary", "big"])));
        Assert.False(s.Matches(new StyleTarget("Label", null, ["primary", "big"], StyleState.Hover)));
    }

    /// <summary>
    /// Verifies that universal and ID selectors match correctly and have expected specificity.
    /// </summary>
    [Fact]
    public void Selector_Universal_And_Id()
    {
        Assert.True(Selector.Parse("*").Matches(new StyleTarget("Anything", null, [])));

        var id = Selector.Parse("#save");
        Assert.True(id.Matches(new StyleTarget("Button", "save", [])));
        Assert.False(id.Matches(new StyleTarget("Button", "load", [])));
        Assert.True(id.Specificity > Selector.Parse(".save").Specificity);
        Assert.True(Selector.Parse(".save").Specificity > Selector.Parse("Button").Specificity);
    }

    /// <summary>
    /// Verifies malformed combinators are rejected.
    /// </summary>
    [Fact]
    public void Selector_RejectsMalformedCombinators()
    {
        Assert.Throws<FormatException>(() => Selector.Parse("> Button"));
        Assert.Throws<FormatException>(() => Selector.Parse("Panel >"));
        Assert.Throws<FormatException>(() => Selector.Parse("Panel >> Button"));
    }

    /// <summary>
    /// Verifies that a style sheet parses comments, variables, and multi-selector rules correctly.
    /// </summary>
    [Fact]
    public void Sheet_Parse_Comments_Variables_MultiSelector()
    {
        var sheet = StyleSheet.Parse("""
            /* a comment */
            --accent: #4a90e2;
            .btn, Button {
              background-color: var(--accent);
              padding: 8 16;   /* inline comment */
            }
            #save { border-width: 2; }
            """);

        Assert.Equal(2, sheet.Rules.Count);
        Assert.Equal("#4a90e2", sheet.Variables["--accent"]);
        Assert.Equal("var(--accent)", sheet.Rules[0].Declarations["background-color"]);
        Assert.Equal("#4a90e2", sheet.ExpandVariables("var(--accent)"));
        Assert.Equal(2, sheet.Rules[0].Selectors.Count);
    }

    /// <summary>Nested selectors expand against each parent selector, including ampersand modifiers.</summary>
    [Fact]
    public void Sheet_Parse_NestedRules()
    {
        var sheet = StyleSheet.Parse("""
            .panel, Dialog {
                padding: 8;
                > Button { width: 40; }
                &:disabled { opacity: 0.5; }
            }
            """);

        Assert.Equal(3, sheet.Rules.Count);
        var child = new StyleTarget("Button", null, [], Ancestors: [new StyleTarget("Panel", null, ["panel"])]);
        Assert.Equal("40", StyleResolver.Resolve([sheet], child).Get("width"));
        Assert.Equal("0.5", StyleResolver.Resolve([sheet],
            new StyleTarget("Panel", null, ["panel"], StyleState.Disabled)).Get("opacity"));
    }

    /// <summary>
    /// Verifies that an unterminated rule throws a format exception.
    /// </summary>
    [Fact]
    public void Sheet_UnterminatedRule_Throws()
    {
        Assert.Throws<FormatException>(() => StyleSheet.Parse(".x { color: red "));
    }
}

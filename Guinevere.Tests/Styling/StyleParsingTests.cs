namespace Guinevere.Tests.Styling;

/// <summary>Tests for <see cref="StyleValue"/>, <see cref="Selector"/> and <see cref="StyleSheet.Parse"/>.</summary>
public class StyleParsingTests
{
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
    /// Verifies that hex and rgba colour strings are parsed correctly.
    /// </summary>
    [Fact]
    public void Value_Colour_Hex_And_Rgb()
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

        Assert.True(s.Matches(new StyleTarget("Button", null, new[] { "primary", "big", "x" }, StyleState.Hover)));
        Assert.False(s.Matches(new StyleTarget("Button", null, new[] { "primary", "big" })));
        Assert.False(s.Matches(new StyleTarget("Label", null, new[] { "primary", "big" }, StyleState.Hover)));
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
    /// Verifies that selectors containing combinators are rejected.
    /// </summary>
    [Fact]
    public void Selector_RejectsCombinators()
    {
        Assert.Throws<FormatException>(() => Selector.Parse("Panel > Button"));
        Assert.Throws<FormatException>(() => Selector.Parse("Panel Button"));
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

    /// <summary>
    /// Verifies that an unterminated rule throws a format exception.
    /// </summary>
    [Fact]
    public void Sheet_UnterminatedRule_Throws()
    {
        Assert.Throws<FormatException>(() => StyleSheet.Parse(".x { color: red "));
    }
}

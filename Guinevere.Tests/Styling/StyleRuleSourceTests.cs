namespace Guinevere.Tests.Styling;

/// <summary>Tests for rule source lines (<see cref="StyleRule.Lines"/>) and <see cref="StyleResolver.MatchedRules"/>.</summary>
public class StyleRuleSourceTests
{
    /// <summary>A rule records its selector line and its own declaration lines, not those of nested rules.</summary>
    [Fact]
    public void Lines_CoverSelectorAndOwnDeclarations()
    {
        var sheet = StyleSheet.Parse("""
            // comment
            button {
                color = #ff0000; width = 10;

                :hover {
                    color = #0000ff;
                }
                height = 20;
            }
            """);

        var button = sheet.Rules.Single(r => r.Selectors[0].Modifier == StyleState.None);
        var hover = sheet.Rules.Single(r => r.Selectors[0].Modifier == StyleState.Hover);

        Assert.Equal([2, 3, 8], button.Lines);
        Assert.Equal([5, 6], hover.Lines);
    }

    /// <summary>Imported rules keep the lines of their own source text.</summary>
    [Fact]
    public void Lines_SurviveImports()
    {
        var options = new StyleSheetOptions
        {
            ImportResolver = id => id == "base" ? new StyleSheetText("\n\nlabel { color = #000000; }") : null,
        };
        var sheet = StyleSheet.Parse("@import \"base\";\nbox { width = 1; }", options);

        Assert.Equal([3], sheet.Rules.Single(r => r.Declarations.ContainsKey("color")).Lines);
        Assert.Equal([2], sheet.Rules.Single(r => r.Declarations.ContainsKey("width")).Lines);
    }

    /// <summary>Matched rules come with their sheet, in cascade order (specificity, then sheet, then rule order).</summary>
    [Fact]
    public void MatchedRules_ListSheetsInCascadeOrder()
    {
        var low = StyleSheet.Parse("button.primary { color = #ff0000; } button { color = #00ff00; } label { color = #000000; }");
        var high = StyleSheet.Parse("button { color = #0000ff; }");
        var gui = new Gui();
        gui.StyleSheets.Add(low);
        gui.StyleSheets.Add(high);

        var matches = gui.StyleSheets.MatchedRules(new StyleTarget("button", null, ["primary"]));

        Assert.Equal([low, high, low], matches.Select(m => m.Sheet));
        Assert.Equal(["button", "button", "button.primary"],
            matches.Select(m => m.Rule.Declarations["color"] switch
            {
                "#00ff00" => "button",
                "#0000ff" => "button",
                _ => "button.primary",
            }));
    }

    /// <summary>Line lookup stays linear on large sheets (a binary search over line starts).</summary>
    [Fact]
    public void Lines_ScaleToLargeSheets()
    {
        var text = string.Join('\n', Enumerable.Range(0, 5000).Select(i => $"r{i} {{ width = {i}; }}"));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var sheet = StyleSheet.Parse(text);

        Assert.Equal([5000], sheet.Rules[^1].Lines);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }
}

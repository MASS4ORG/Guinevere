using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Controls reserve the width of their styled scope font and its fallback glyphs.</summary>
public class FontMeasurementTests
{
    /// <summary>Checkboxes and tabs reserve the width of their styled scope fonts and fallback glyphs.</summary>
    [Fact]
    public void CheckboxAndTabsMeasureTheirStyledFonts()
    {
        using var frame = new FrameHarness();
        frame.Gui.Fonts.RegisterFile("Brand", FontRegistryTests.FontPath());
        frame.Gui.Fonts.RegisterFile("Emoji", FontRegistryTests.FontPath("icons.ttf"));
        frame.Gui.StyleSheets.Add(StyleSheet.Parse("checkbox, tab { font-family = Brand, Emoji; }"));
        frame.Frame(gui =>
        {
            gui.Checkbox(false, "WWW😀", size: 16, fontSize: 18, spacing: 6);
            var checkbox = gui.CurrentNode.Children.First();
            Assert.Equal(22 + gui.MeasureTextWidth("WWW😀", 18, checkbox.Scope), checkbox.Style.Width);
            gui.Tabs(0, builder => builder.Tab("WWW😀"), fontSize: 18);
            var tabs = gui.CurrentNode.Children[1];
            var tab = tabs.Children.First().Children.First();
            Assert.Equal(24 + gui.MeasureTextWidth("WWW😀", 18, tab.Scope), tab.Style.Width);
        });
    }

    /// <summary>Pill tabs and strip tabs measure child font rules, including fallback and weight declarations.</summary>
    [Fact]
    public void PillAndStripTabsMeasureTheirStyledFonts()
    {
        using var frame = new FrameHarness();
        frame.Gui.Fonts.RegisterFile("Brand", FontRegistryTests.FontPath());
        frame.Gui.Fonts.RegisterFile("Emoji", FontRegistryTests.FontPath("icons.ttf"));
        frame.Gui.StyleSheets.Add(StyleSheet.Parse(
            "tabbar > tab, tabstrip > tab { font-family = Brand, Emoji; font-weight = 700; font-style = italic; }"));
        var selected = 0;
        frame.Frame(gui =>
        {
            gui.PillTabs(ref selected, builder => builder.Tab("WWW😀"), fontSize: 18);
            var pill = gui.CurrentNode.Children[0].Children.First().Children.First();
            Assert.Equal(24 + gui.MeasureTextWidth("WWW😀", 18, pill.Scope), pill.Style.Width);
            gui.TabStrip([new TabStripItem("one", "WWW😀", Closable: false)], "one");
            var strip = gui.CurrentNode.Children[1];
            var tab = strip.Children.First();
            Assert.Equal(18 + gui.MeasureTextWidth("WWW😀", TabStripTheme.Default.FontSize, tab.Scope),
                tab.Style.Width);
            Assert.True(tab.Scope.Get<LayoutNodeScopeTextFont>().Value.Italic);
            Assert.Throws<ArgumentNullException>(() => gui.GetStyleFont(null!));
            Assert.Same(gui.CurrentNodeScope.Get<LayoutNodeScopeTextFont>().Value,
                gui.GetStyleFont(gui.ResolveStyle("unknown-control")));
        });
    }
}

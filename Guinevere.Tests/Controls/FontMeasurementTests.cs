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
        frame.Gui.StyleSheets.Add(StyleSheet.Parse(
            "checkbox, tab { font-family = Brand, Emoji; font-size = 18; }"));
        LayoutNode? checkbox = null;
        LayoutNode? tab = null;
        var labelWidth = 0f;
        var tabTextWidth = 0f;
        frame.Frame(gui =>
        {
            var on = false;
            checkbox = gui.Checkbox(ref on, "WWW😀");
            labelWidth = gui.MeasureTextWidth("WWW😀", 18, checkbox.Scope);
            var active = 0;
            tab = gui.Tabs(ref active, static builder => builder.Tab("WWW😀")).Children[0].Children[0];
            tabTextWidth = gui.MeasureTextWidth("WWW😀", 18, tab.Scope);
        });

        var (indicator, label) = (checkbox!.Children[0], checkbox.Children[1]);
        Assert.Equal(labelWidth, label.Rect.W, 0.5f);
        Assert.Equal(indicator.Rect.W + frame.Gui.ControlStyle.Spacing + labelWidth, checkbox.Rect.W, 0.5f);
        Assert.Equal(24 + tabTextWidth, tab!.Rect.W, 0.5f);
    }

    /// <summary>Pill tabs and strip tabs measure child font rules, including fallback and weight declarations.</summary>
    [Fact]
    public void PillAndStripTabsMeasureTheirStyledFonts()
    {
        using var frame = new FrameHarness();
        frame.Gui.Fonts.RegisterFile("Brand", FontRegistryTests.FontPath());
        frame.Gui.Fonts.RegisterFile("Emoji", FontRegistryTests.FontPath("icons.ttf"));
        frame.Gui.StyleSheets.Add(StyleSheet.Parse(
            "tabbar > tab, tabstrip > tab { font-family = Brand, Emoji; font-weight = 700; font-style = italic; }"
            + " tabbar > tab { font-size = 18; }"));
        var selected = 0;
        LayoutNode? pill = null;
        var pillTextWidth = 0f;
        frame.Frame(gui =>
        {
            pill = gui.PillTabs(ref selected, builder => builder.Tab("WWW😀")).Children[0].Children[0];
            pillTextWidth = gui.MeasureTextWidth("WWW😀", 18, pill.Scope);
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

        Assert.Equal(24 + pillTextWidth, pill!.Rect.W, 0.5f);
    }
}

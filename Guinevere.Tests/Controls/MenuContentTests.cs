using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>Menu titles and items take any content, are keyed by their label, and the bar reports an item's action.</summary>
public class MenuContentTests
{
    [Fact]
    public void ContentTitlesAndItemsOpenAndRunAndTheBarReportsTheClick()
    {
        using var harness = new FrameHarness(600, 400);
        var runs = 0;
        var clicks = 0;
        void Draw(Gui gui)
        {
            var bar = gui.MenuBar(menus => menus.Menu(Content.Row(Icon.FromGlyph("F"), "File"), file => file
                .Item(Icon.FromGlyph("+").Labeled("Add"), () => runs++)
                .Item("Close", () => runs += 10)), id: "bar");
            if (gui.Pass == Pass.Pass2Render && bar.Clicked)
            {
                clicks++;
            }
        }

        harness.Frame(Draw);
        var title = harness.Gui.RootNode!.FindChildById("bar")!.Children[0];
        harness.Click(Draw, FrameHarness.Center(title));
        harness.Frame(Draw);

        var row = harness.Gui.RootNode!.FindChildById("/menubar/File/v0/i0");
        Assert.NotNull(row);
        harness.Click(Draw, FrameHarness.Center(row));

        Assert.Equal(1, runs);
        Assert.Equal(1, clicks);
        Assert.Null(harness.Gui.RootNode!.FindChildById("/menubar/File/v0"));
    }

    [Fact]
    public void AnIconOnlyItemIsKeyedByItsPositionAndLabeledForAssistiveTools()
    {
        var builder = new FlyoutBuilder();
        builder.Item(Icon.FromGlyph("+").Labeled("Add")).Item(Icon.FromGlyph("-")).Separator();

        Assert.Equal(["Add", "#1", "#2"], builder.Items.Select(static item => item.Key));
        Assert.Equal("Add", builder.Items[0].Content.Label);
    }
}

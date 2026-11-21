using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>The shared choice row: fits its parts, lets the sheet size the indicator, and takes any content.</summary>
public class ChoiceControlsTests
{
    [Fact]
    public void SheetSizesTheIndicatorAndTheLabelCanBeAnIcon()
    {
        using var harness = new FrameHarness();
        harness.Gui.StyleSheets.Add(StyleSheet.Parse("checkbox > indicator { width = 30; height = 30; }"));
        LayoutNode? box = null, toggle = null;
        harness.Frame(g =>
        {
            var on = true;
            box = g.Checkbox(ref on, Icon.FromGlyph("+").Labeled("Add"));
            toggle = g.Toggle(ref on);
        });

        Assert.Equal(30f, box!.Children[0].Rect.W);
        Assert.Equal(2, box.Children.Count);
        Assert.Equal(box.Children[0].Rect.W + harness.Gui.ControlStyle.Spacing + box.Children[1].Rect.W,
            box.Rect.W, 0.5f);
        Assert.Single(toggle!.Children);
    }
}

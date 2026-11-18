using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Styling;

public class StyleLayoutApplyTests
{
    static LayoutNode Apply(string declarations)
    {
        var gui = new TestableGui();
        gui.SetStage(Pass.Pass1Build);
        var root = LayoutNode.CreateRoot(gui, 500, 500);
        var node = new LayoutNode("box", gui, root);
        root.AddChild(node);
        var style = StyleResolver.Resolve([StyleSheet.Parse($"box {{ {declarations} }}")],
            new StyleTarget("box", null, []));
        StyleLayout.Apply(node, style);
        return node;
    }

    [Fact]
    public void SizesAndBoxesUseTheirDeclaredUnitsAndSideOrder()
    {
        var node = Apply("""
            width = 50%; height = ratio(2); min-width = 10px; max-width = 200px;
            min-height = 15px; max-height = 300px;
            padding = 1px 2px 3px 4px; margin = 5px 6px;
            """);

        Assert.Equal(Size.Percentage(0.5f), node.Style.Width);
        Assert.Equal(Size.Ratio(2f), node.Style.Height);
        Assert.Equal((Size.Pixels(10f), Size.Pixels(200f), Size.Pixels(15f), Size.Pixels(300f)),
            (node.Style.MinWidth!.Value, node.Style.MaxWidth!.Value, node.Style.MinHeight!.Value,
                node.Style.MaxHeight!.Value));
        Assert.Equal((Size.Pixels(1f), Size.Pixels(2f), Size.Pixels(3f), Size.Pixels(4f)),
            (node.Style.PaddingTop, node.Style.PaddingRight, node.Style.PaddingBottom, node.Style.PaddingLeft));
        Assert.Equal((Size.Pixels(5f), Size.Pixels(6f), Size.Pixels(5f), Size.Pixels(6f)),
            (node.Style.MarginTop, node.Style.MarginRight, node.Style.MarginBottom, node.Style.MarginLeft));
    }

    [Fact]
    public void SizesComposeTermsAndBoxesAcceptRelativeSides()
    {
        var node = Apply("width = 50% + 8px; min-height = fit - 4px; gap = expand; padding = 10% 2px + ratio(0.1);");

        Assert.Equal(Size.Percentage(0.5f) + 8f, node.Style.Width);
        Assert.Equal(Size.Fit - Size.Pixels(4f), node.Style.MinHeight);
        Assert.Equal(Size.Expand(), node.Style.Gap);
        Assert.Equal(Size.Percentage(0.1f), node.Style.PaddingTop);
        Assert.Equal(Size.Pixels(2f) + Size.Ratio(0.1f), node.Style.PaddingLeft);
    }

    [Fact]
    public void FlexAndTextOptionsApplyTogether()
    {
        var node = Apply("""
            flex-direction = row-reverse; flex-wrap = wrap; flex-grow = 1; gap = 7;
            align-self = center; align-items = end; justify-content = start;
            text-wrap = character; line-height = 1.5; max-lines = 3; text-ellipsis = "more";
            """);
        var text = node.Scope.Get<LayoutNodeScopeTextLayout>().Value;

        Assert.Equal(Axis.Horizontal, node.Style.Direction);
        Assert.True(node.Style.Wrap);
        Assert.True(node.Style.ExpandsWidth && node.Style.ExpandsHeight);
        Assert.Equal(Size.Pixels(7f), node.Style.Gap);
        Assert.Equal((Size.Percentage(0.5f), Size.Percentage(1f), Size.Percentage(0f)),
            (node.Style.AlignSelf!.Value, node.Style.AlignContentHorizontal, node.Style.AlignContentVertical));
        Assert.Equal(TextWrapMode.Character, text.WrapMode);
        Assert.Equal(1.5f, text.LineHeight);
        Assert.Equal(3, text.MaxLines);
        Assert.Equal("more", text.Ellipsis);
    }

    [Fact]
    public void InvalidValuesLeaveExistingLayoutUntouched()
    {
        var node = Apply("width = nonsense; height = ratio(oops); padding = 1px bad; gap = no; line-height = -2;");

        Assert.Null(node.Style.Width);
        Assert.Null(node.Style.Height);
        Assert.True(node.Style.PaddingLeft.IsZero);
        Assert.True(node.Style.Gap.IsZero);
        Assert.Equal(1.2f, node.Scope.Get<LayoutNodeScopeTextLayout>().Value.LineHeight);
    }

    [Fact]
    public void ExpandAndFitKeywordsProduceComposableSizes()
    {
        var expanded = Apply("width = expand; height = auto; padding = 4px;");
        var fitted = Apply("width = fit; height = 20px;");

        Assert.Equal(1f, expanded.Style.Width!.Value.ExpandContribution);
        Assert.Equal(1f, expanded.Style.Height!.Value.FitContentContribution);
        Assert.Equal((4f, 4f, 4f, 4f),
            (expanded.PaddingTopPixels, expanded.PaddingRightPixels,
                expanded.PaddingBottomPixels, expanded.PaddingLeftPixels));
        Assert.Equal(1f, fitted.Style.Width!.Value.FitContentContribution);
        Assert.Equal(Size.Pixels(20f), fitted.Style.Height);
    }
}

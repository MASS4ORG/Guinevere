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
            width: 50%; height: ratio(2); min-width: 10px; max-width: 200px;
            min-height: 15px; max-height: 300px;
            padding: 1px 2px 3px 4px; margin: 5px 6px;
            """);

        Assert.Equal(0.5f, node.Style.WidthPercent);
        Assert.Equal(2f, node.Style.HeightExpression!.Value.RatioContribution);
        Assert.Equal((10f, 200f, 15f, 300f),
            (node.Style.MinWidth, node.Style.MaxWidth, node.Style.MinHeight, node.Style.MaxHeight));
        Assert.Equal((1f, 2f, 3f, 4f),
            (node.Style.PaddingTop, node.Style.PaddingRight, node.Style.PaddingBottom, node.Style.PaddingLeft));
        Assert.Equal((5f, 6f, 5f, 6f),
            (node.Style.MarginTop, node.Style.MarginRight, node.Style.MarginBottom, node.Style.MarginLeft));
    }

    [Fact]
    public void FlexAndTextOptionsApplyTogether()
    {
        var node = Apply("""
            flex-direction: row-reverse; flex-wrap: wrap; flex-grow: 1; gap: 7;
            align-self: center; align-items: end; justify-content: start;
            text-wrap: character; line-height: 1.5; max-lines: 3; text-ellipsis: "more";
            """);
        var text = node.Scope.Get<LayoutNodeScopeTextLayout>().Value;

        Assert.Equal(Axis.Horizontal, node.Style.Direction);
        Assert.True(node.Style.Wrap);
        Assert.True(node.Style.IsExpanded);
        Assert.Equal(7f, node.Style.Gap);
        Assert.Equal((0.5f, 1f, 0f),
            (node.Style.AlignSelf, node.Style.AlignContentHorizontal, node.Style.AlignContentVertical));
        Assert.Equal(TextWrapMode.Character, text.WrapMode);
        Assert.Equal(1.5f, text.LineHeight);
        Assert.Equal(3, text.MaxLines);
        Assert.Equal("more", text.Ellipsis);
    }

    [Fact]
    public void InvalidValuesLeaveExistingLayoutUntouched()
    {
        var node = Apply("width: nonsense; height: ratio(oops); padding: 1px bad; gap: no; line-height: -2;");

        Assert.Null(node.Style.WidthExpression);
        Assert.Null(node.Style.HeightExpression);
        Assert.Equal(0f, node.Style.PaddingLeft);
        Assert.Equal(0f, node.Style.Gap);
        Assert.Equal(1.2f, node.Scope.Get<LayoutNodeScopeTextLayout>().Value.LineHeight);
    }

    [Fact]
    public void ExpandAndFitKeywordsProduceComposableSizes()
    {
        var expanded = Apply("width: expand; height: auto; padding: 4px;");
        var fitted = Apply("width: fit; height: 20px;");

        Assert.Equal(1f, expanded.Style.WidthExpression!.Value.ExpandContribution);
        Assert.Equal(1f, expanded.Style.HeightExpression!.Value.FitContentContribution);
        Assert.Equal((4f, 4f, 4f, 4f),
            (expanded.Style.PaddingTop, expanded.Style.PaddingRight,
                expanded.Style.PaddingBottom, expanded.Style.PaddingLeft));
        Assert.Equal(1f, fitted.Style.WidthExpression!.Value.FitContentContribution);
        Assert.Equal(20f, fitted.Style.Height);
    }
}

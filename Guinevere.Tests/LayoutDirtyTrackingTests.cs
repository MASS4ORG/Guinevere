namespace Guinevere.Tests;

/// <summary>Tests retained-tree layout invalidation and no-change reuse.</summary>
public class LayoutDirtyTrackingTests : LayoutNodeTestBase
{
    [Fact]
    public void CalculateLayoutUnchangedTreeReusesPreviousLayout()
    {
        var gui = CreateTestGui(400, 200);
        var child = CreateTestLayoutNode(gui, gui.RootNode, 100f, 20f);
        gui.RootNode!.AddChild(child);

        gui.RootNode.CalculateLayout();
        var version = gui.RootNode.LayoutVersion;
        gui.RootNode.CalculateLayout();

        Assert.Equal(version, gui.RootNode.LayoutVersion);
    }

    [Fact]
    public void FluentSizeChangeInvalidatesRetainedTree()
    {
        var gui = CreateTestGui(400, 200);
        SetGuiStage(gui, Pass.Pass1Build);
        var child = CreateTestLayoutNode(gui, gui.RootNode, 100f, 20f);
        gui.RootNode!.AddChild(child);
        gui.RootNode.CalculateLayout();
        var version = gui.RootNode.LayoutVersion;

        child.Width(150f);
        gui.RootNode.CalculateLayout();

        Assert.Equal(version + 1, gui.RootNode.LayoutVersion);
        Assert.Equal(150f, child.Rect.W);
    }

    [Fact]
    public void DirectStyleChangeRecalculatesAfterExplicitInvalidation()
    {
        var gui = CreateTestGui(400, 200);
        var child = CreateTestLayoutNode(gui, gui.RootNode, 100f, 20f);
        gui.RootNode!.AddChild(child);
        gui.RootNode.CalculateLayout();

        child.Style.Width = 175f;
        child.InvalidateLayout();
        gui.RootNode.CalculateLayout();

        Assert.Equal(175f, child.Rect.W);
    }
}

namespace Guinevere.Tests.Layout;

/// <summary>Tests for value inheritance through <see cref="LayoutNodeScope"/> across the two passes.</summary>
public class LayoutNodeScopeTests
{
    /// <summary>
    /// Runs a frame; <paramref name="draw"/> receives whether it is the render pass and returns the child scope.
    /// </summary>
    static LayoutNodeScope Frame(Func<Gui, bool, LayoutNodeScope> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw(gui, false);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        var scope = draw(gui, true);
        gui.Render();
        gui.EndFrame();
        return scope;
    }

    /// <summary>
    /// A value the parent changes in the render pass (a hover color) reaches a child that set a different value of
    /// its own, while the child's own value is kept.
    /// </summary>
    [Fact]
    public void RenderPassParentValue_ReachesChildrenThatSetOtherValues()
    {
        var scope = Frame((gui, render) =>
        {
            using (gui.Node(40, 40).Enter())
            {
                gui.SetTextColor(render ? Color.Red : Color.Green);
                using (gui.Node(10, 10).Enter())
                {
                    gui.SetTextSize(20);
                    using (gui.Node(5, 5).Enter()) return gui.CurrentNodeScope;
                }
            }
        });

        Assert.Equal(Color.Red, scope.Get<LayoutNodeScopeTextColor>().Value);
        Assert.Equal(20f, scope.Get<LayoutNodeScopeTextSize>().Value);
    }

    /// <summary>Nodes created from <see cref="UnitValue"/> sizes re-inherit in the render pass too.</summary>
    [Fact]
    public void UnitValueNodes_ReinheritInTheRenderPass()
    {
        var scope = Frame((gui, render) =>
        {
            using (gui.Node(UnitValue.Pixels(40), UnitValue.Pixels(40)).Enter())
            {
                gui.SetTextColor(render ? Color.Red : Color.Green);
                using (gui.Node(UnitValue.Percentage(0.5f), UnitValue.Pixels(10)).Enter())
                {
                    gui.SetTextSize(20);
                    return gui.CurrentNodeScope;
                }
            }
        });

        Assert.Equal(Color.Red, scope.Get<LayoutNodeScopeTextColor>().Value);
    }

    /// <summary>A child's own value still wins over the parent's, in both passes.</summary>
    [Fact]
    public void ChildValue_WinsOverParent()
    {
        var scope = Frame((gui, render) =>
        {
            using (gui.Node(40, 40).Enter())
            {
                gui.SetTextColor(render ? Color.Red : Color.Green);
                using (gui.Node(10, 10).Enter())
                {
                    gui.SetTextColor(Color.Blue);
                    return gui.CurrentNodeScope;
                }
            }
        });

        Assert.Equal(Color.Blue, scope.Get<LayoutNodeScopeTextColor>().Value);
    }
}

using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Layout;

/// <summary>
/// Covers nodes that opt out of their parent's flow. Before absolute positioning existed,
/// <see cref="LayoutNode.Left"/>/<see cref="LayoutNode.Top"/> wrote straight to the node rect and
/// <c>CalculateLayout</c> then overwrote it, so every overlay in the library drew at its flow
/// position instead of where it asked to be.
/// </summary>
public class AbsolutePositioningTests
{
    static Gui RunLayout(Action<Gui> draw, int width = 400, int height = 300)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-1, -1));
        input.PrevMousePosition.Returns(new Vector2(-1, -1));

        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(width, height);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw(gui);
        gui.CalculateLayout();
        return gui;
    }

    static LayoutNode Find(Gui gui, string id) =>
        gui.RootNode!.FindChildById(id) ?? throw new InvalidOperationException($"node '{id}' not found");

    [Fact]
    public void AbsoluteChildIsPlacedRelativeToTheParentContentBox()
    {
        var gui = RunLayout(g =>
        {
            using (g.Node(200, 100, "host").Left(50).Top(20).Padding(10).Enter())
            {
                g.Node(30, 30, "target").Absolute(5, 7);
            }
        });

        var target = Find(gui, "target");
        // host sits at (50,20) with 10px padding, so its content box starts at (60,30)
        Assert.Equal(65f, target.Rect.X);
        Assert.Equal(37f, target.Rect.Y);
        Assert.Equal(30f, target.Rect.W);
        Assert.Equal(30f, target.Rect.H);
    }

    [Fact]
    public void AbsoluteScreenChildIgnoresWhereItSitsInTheTree()
    {
        var gui = RunLayout(g =>
        {
            using (g.Node(200, 100, "host").Left(50).Top(20).Padding(10).Enter())
            {
                g.Node(30, 30, "target").AbsoluteScreen(5, 7);
            }
        });

        var target = Find(gui, "target");
        Assert.Equal(5f, target.Rect.X);
        Assert.Equal(7f, target.Rect.Y);
    }

    [Fact]
    public void AbsoluteChildDoesNotTakeSpaceFromItsFlowSiblings()
    {
        var gui = RunLayout(g =>
        {
            using (g.Node(200, 300, "host").Enter())
            {
                g.Node(40, 20, "first");
                g.Node(40, 20, "overlay").Absolute(0, 0);
                g.Node(40, 20, "second");
            }
        });

        // Without the absolute node in the flow, 'second' stacks directly under 'first'.
        Assert.Equal(0f, Find(gui, "first").Rect.Y);
        Assert.Equal(20f, Find(gui, "second").Rect.Y);
    }

    [Fact]
    public void AbsoluteChildSizesAgainstTheBoxItIsPositionedIn()
    {
        var gui = RunLayout(g =>
        {
            using (g.Node(200, 100, "host").Enter())
            {
                g.Node(-1, -1, "half").WidthPercent(0.5f).HeightPercent(0.5f).Absolute(0, 0);
                g.Node(-1, -1, "screenHalf").WidthPercent(0.5f).HeightPercent(0.5f).AbsoluteScreen(0, 0);
            }
        });

        Assert.Equal(100f, Find(gui, "half").Rect.W);
        Assert.Equal(50f, Find(gui, "half").Rect.H);
        Assert.Equal(200f, Find(gui, "screenHalf").Rect.W);
        Assert.Equal(150f, Find(gui, "screenHalf").Rect.H);
    }

    [Fact]
    public void AbsoluteChildIsNotCarriedByAnAncestorScrollOffset()
    {
        var gui = RunLayout(g =>
        {
            using (g.Node(200, 100, "scroller").Enter())
            {
                g.ScrollY();
                g.SetLocalScrollOffset(new Vector2(0, 40), g.CurrentNode.Scope);
                g.Node(40, 20, "flow");
                g.Node(40, 20, "pinned").Absolute(0, 0);
            }
        });

        Assert.Equal(-40f, Find(gui, "flow").Rect.Y);
        Assert.Equal(0f, Find(gui, "pinned").Rect.Y);
    }

    [Fact]
    public void ParentWithOnlyAbsoluteChildrenStillLaysThemOut()
    {
        var gui = RunLayout(g =>
        {
            using (g.Node(200, 100, "host").Left(10).Top(10).Enter())
            {
                g.Node(30, 30, "only").Absolute(4, 6);
            }
        });

        Assert.Equal(14f, Find(gui, "only").Rect.X);
        Assert.Equal(16f, Find(gui, "only").Rect.Y);
    }

    [Fact]
    public void LeftAndTopMarkTheNodeAbsolute()
    {
        var gui = RunLayout(g =>
        {
            using (g.Node(200, 100, "host").Enter())
            {
                g.Node(40, 20, "first");
                g.Node(40, 20, "offset").Left(12).Top(34);
            }
        });

        var offset = Find(gui, "offset");
        Assert.True(offset.Style.IsAbsolute);
        Assert.Equal(12f, offset.Rect.X);
        Assert.Equal(34f, offset.Rect.Y);
    }
}

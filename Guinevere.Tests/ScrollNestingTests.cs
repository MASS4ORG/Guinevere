using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>
/// A scroll container must move its content once, however deeply nested. The offset used to be summed
/// at every ancestor level — <c>LayoutNodeScope.Get</c> inherits — so a grandchild moved twice as far
/// as its parent and vanished off screen.
/// </summary>
public class ScrollNestingTests
{
    static Gui RunFrame(float scrollY, out LayoutNode child, out LayoutNode grandchild)
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));

        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(200, 200);

        void Draw()
        {
            using (gui.Node(200, 200, "scroller").Enter())
            {
                gui.ScrollY();
                gui.SetLocalScrollOffset(new Vector2(0, scrollY), gui.CurrentNode.Scope);

                using (gui.Node(180, 40, "child").Enter())
                {
                    using (gui.Node(160, 20, "grandchild").Enter())
                    {
                        using (gui.Node(140, 10, "greatGrandchild").Enter())
                        {
                        }
                    }
                }
            }
        }

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Draw();
        gui.CalculateLayout();

        child = gui.RootNode!.FindChildById("child")!;
        grandchild = gui.RootNode!.FindChildById("grandchild")!;
        return gui;
    }

    [Fact]
    public void EveryDepthMovesByTheSameScrollOffset()
    {
        RunFrame(scrollY: 0f, out var restingChild, out var restingGrandchild);
        var restingGreat = 0f;

        var gui = RunFrame(scrollY: 100f, out var child, out var grandchild);
        var great = gui.RootNode!.FindChildById("greatGrandchild")!;

        RunFrame(scrollY: 0f, out _, out _);

        Assert.Equal(-100f, child.Rect.Y - restingChild.Rect.Y, 1);
        Assert.Equal(-100f, grandchild.Rect.Y - restingGrandchild.Rect.Y, 1);
        Assert.Equal(restingGreat + great.Rect.Y, great.Rect.Y, 1);
    }

    [Fact]
    public void ADescendantStaysInsideItsParentWhileScrolled()
    {
        var gui = RunFrame(scrollY: 100f, out var child, out var grandchild);
        var great = gui.RootNode!.FindChildById("greatGrandchild")!;

        Assert.True(grandchild.Rect.Y >= child.Rect.Y,
            $"grandchild at {grandchild.Rect.Y} escaped its parent at {child.Rect.Y}");
        Assert.True(great.Rect.Y >= grandchild.Rect.Y,
            $"great-grandchild at {great.Rect.Y} escaped its parent at {grandchild.Rect.Y}");
    }
}

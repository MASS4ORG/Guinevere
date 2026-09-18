using Guinevere.Tests.Mocks;

namespace Guinevere.Tests;

/// <summary>
/// Interactables are hit by their full node rect, but drawing is clipped to scroll containers'
/// viewports — so a row that straddles the container's bottom edge kept its whole rect live below
/// the fold, letting a pointer over whatever sits under the panel (a studio status bar) still select
/// it. Hover must refuse a point outside every clipping ancestor.
/// </summary>
public class InteractableClipTests
{
    static Gui RunFrame(out LayoutNode viewport, out LayoutNode row, out IInputHandler input)
    {
        using var surface = SKSurface.Create(new SKImageInfo(200, 200));

        input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));

        var gui = new TestableGui { Input = input };
        gui.SetScreenRect(200, 200);

        void Draw()
        {
            using (gui.Node(200, 120, "viewport").Enter())
            {
                gui.ScrollY();

                using (gui.Node(200, 200, "row").Enter())
                {
                }
            }
        }

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Draw();
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Draw();
        gui.Render();
        gui.EndFrame();

        viewport = gui.RootNode!.FindChildById("viewport")!;
        row = gui.RootNode!.FindChildById("row")!;
        return gui;
    }

    [Fact]
    public void HoverInsideTheViewportTouchesTheRowButOutsideItDoesNot()
    {
        var gui = RunFrame(out var viewport, out var row, out var input);

        Assert.True(row.Rect.Y + row.Rect.H > viewport.Rect.Y + viewport.Rect.H,
            $"row ({row.Rect}) should straddle the viewport bottom ({viewport.Rect})");

        input.MousePosition.Returns(new Vector2(10, viewport.Rect.Y + viewport.Rect.H - 5));
        Assert.True(gui.GetInteractable(row).OnHover());

        input.MousePosition.Returns(new Vector2(10, viewport.Rect.Y + viewport.Rect.H + 5));
        Assert.False(gui.GetInteractable(row).OnHover());
    }
}

using Guinevere.Tests.Mocks;

namespace Guinevere.Tests.Controls;

/// <summary>
/// Covers the node-attached tooltip: it stays invisible until the pointer has hovered the anchor
/// node for the configured delay, appears only while the pointer remains inside the node, and never
/// shows without a hover. Visibility is asserted by sampling the tooltip panel's background pixels
/// (the panel is only drawn once <c>show</c> is decided in the render pass).
/// </summary>
public class TooltipTests
{
    const int Width = 400;
    const int Height = 400;
    const int SurfaceWidth = 400;
    const int SurfaceHeight = 400;
    const float Delay = 0.5f;

    static readonly Vector2 PointerInsideAnchor = new(50, 50);
    static readonly Vector2 PointerOutside = new(350, 350);

    static Gui CreateGui()
    {
        var gui = new TestableGui { Input = NoInput() };
        gui.SetScreenRect(Width, Height);
        return gui;
    }

    static IInputHandler At(Vector2 position)
    {
        var input = NoInput();
        input.MousePosition.Returns(position);
        input.PrevMousePosition.Returns(position);
        return input;
    }

    /// <summary>
    /// The node + tooltip content. Called once in the build pass and once in the render pass — same
    /// source lines — so the node tree and the tooltip's control state are shared across passes, as
    /// in a real application frame.
    /// </summary>
    static void Content(Gui gui, string text)
    {
        using (gui.Node(100, 100).Enter())
        {
            gui.Tooltip(gui.CurrentNode, text, delay: Delay);
        }
    }

    /// <summary>
    /// Runs one full frame. The pointer is handled by the caller via <c>gui.Input</c>.
    /// Returns the anchor so tests can size the panel's expected position.
    /// </summary>
    static LayoutNode Frame(Gui gui, SKCanvas canvas, string text, bool animated = true)
    {
        canvas.Clear(SKColors.Transparent);

        gui.Time.Update(animated ? 0.016f : 0f);
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        Content(gui, text);
        gui.CalculateLayout();

        gui.SetStage(Pass.Pass2Render);
        Content(gui, text);
        gui.Render();
        gui.EndFrame();

        return gui.RootNode!.ChildNodes[0];
    }

    static SKColor PixelAt(SKSurface surface, int x, int y)
    {
        surface.Canvas.Flush();
        using var pixmap = surface.PeekPixels();
        return pixmap!.GetPixelColor(x, y);
    }

    static byte BackgroundAlpha(SKSurface surface, Rect tooltipRect)
    {
        // A pixel on the right edge of the panel, away from the left-anchored glyphs and border.
        var example = PixelAt(surface, (int)tooltipRect.X + (int)tooltipRect.W - 5,
            (int)tooltipRect.Y + (int)tooltipRect.H / 2);
        return example.Alpha;
    }

    static IInputHandler NoInput()
    {
        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-100, -100));
        input.PrevMousePosition.Returns(new Vector2(-100, -100));
        input.MouseDelta.Returns(Vector2.Zero);
        input.MouseWheelDelta.Returns(0f);
        input.IsMouseButtonPressed(Arg.Any<MouseButton>()).Returns(false);
        input.IsMouseButtonDown(Arg.Any<MouseButton>()).Returns(false);
        input.IsAnyKeyDown.Returns(false);
        input.IsKeyPressed(Arg.Any<KeyboardKey>()).Returns(false);
        return input;
    }

    [Fact]
    public void TooltipAppearsOnlyAfterTheDelayElapses()
    {
        using var surface = SKSurface.Create(new SKImageInfo(SurfaceWidth, SurfaceHeight));
        var gui = CreateGui();
        gui.Input = At(PointerInsideAnchor);

        // Pointer entered on frame 0, but the delay has not elapsed yet: panel must be invisible.
        var anchor = Frame(gui, surface.Canvas, "Some tooltip text");
        Assert.True(BackgroundAlpha(surface, TooltipRect(anchor)) < 8,
            "Tooltip must not draw before the hover delay has elapsed.");

        // Keep hovering past the delay: the panel fades in.
        for (var i = 0; i < 40; i++)
            Frame(gui, surface.Canvas, "Some tooltip text");

        Assert.True(BackgroundAlpha(surface, TooltipRect(anchor)) > 100,
            "Tooltip must become visible once the pointer has hovered long enough.");
    }

    [Fact]
    public void TooltipHidesWhenThePointerLeaves()
    {
        using var surface = SKSurface.Create(new SKImageInfo(SurfaceWidth, SurfaceHeight));
        var gui = CreateGui();
        gui.Input = At(PointerInsideAnchor);

        var anchor = Frame(gui, surface.Canvas, "Some tooltip text");
        for (var i = 0; i < 40; i++)
            Frame(gui, surface.Canvas, "Some tooltip text");
        Assert.True(BackgroundAlpha(surface, TooltipRect(anchor)) > 100, "precondition: shown");

        // Leave the anchor: the very next frame must stop drawing the panel.
        gui.Input = At(PointerOutside);
        Frame(gui, surface.Canvas, "Some tooltip text");

        Assert.True(BackgroundAlpha(surface, TooltipRect(anchor)) < 8,
            "Tooltip must disappear as soon as the pointer leaves the anchor.");
    }

    [Fact]
    public void TooltipNeverShowsWithoutHover()
    {
        using var surface = SKSurface.Create(new SKImageInfo(SurfaceWidth, SurfaceHeight));
        var gui = CreateGui();
        gui.Input = At(PointerOutside);

        var anchor = Frame(gui, surface.Canvas, "Some tooltip text");
        for (var i = 0; i < 40; i++)
            Frame(gui, surface.Canvas, "Some tooltip text");

        Assert.True(BackgroundAlpha(surface, TooltipRect(anchor)) < 8,
            "Tooltip must never appear while the pointer is not over the anchor.");
    }

    /// <summary>Position of the mouse-follow tooltip given the anchor's height and the forced example pointer.</summary>
    static Rect TooltipRect(LayoutNode anchor)
    {
        var pos = PointerInsideAnchor + new Vector2(0, anchor.Rect.H);
        var font = new SKFont { Size = 12 };
        font.MeasureText("Some tooltip text", out var bounds);
        return new Rect(pos.X, pos.Y, Math.Min(bounds.Width + 16, 200), bounds.Height + 16);
    }
}

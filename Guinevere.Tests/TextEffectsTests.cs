namespace Guinevere.Tests;

/// <summary>
/// Pixel-level tests for <see cref="TextEffects"/> on <see cref="Gui.DrawText(string, float, Color?, Font?, float, bool, bool, TextEffects?)"/>.
/// The <c>ui --sample text</c> CLI verb renders the same effects for eyeballing.
/// </summary>
public class TextEffectsTests
{
    private const int W = 400;
    private const int H = 140;
    private const float Size = 56f;

    private static readonly Font Face = Font.FromFamilyName("sans-serif", Size);

    private static byte[] RenderFrame(Action<Gui> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(W, H, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas, Face, Face);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
        canvas.Flush();

        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        return pixmap.GetPixelSpan().ToArray();
    }

    private static (byte R, byte B, byte A) At(byte[] px, int x, int y)
    {
        var i = ((y * W) + x) * 4;
        return (px[i], px[i + 2], px[i + 3]);
    }

    private static int OpaqueCount(byte[] px)
    {
        var n = 0;
        for (var i = 3; i < px.Length; i += 4)
            if (px[i] > 20)
                n++;
        return n;
    }

    /// <summary>Empty effects render the same as no effects (the refactor is transparent).</summary>
    [Fact]
    public void EmptyEffects_MatchPlainText()
    {
        var plain = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White));
        var empty = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White, effects: new TextEffects()));

        Assert.Equal(plain, empty);
    }

    /// <summary>An outline puts extra ink around the glyphs.</summary>
    [Fact]
    public void Outline_AddsInkAroundGlyphs()
    {
        var plain = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White));
        var outlined = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White, effects: new TextEffects
        {
            Outline = new TextEffects.TextOutline(Color.FromArgb(255, 255, 0, 0), 4f),
        }));

        Assert.True(OpaqueCount(outlined) > OpaqueCount(plain) + 200,
            $"outline should add coverage (plain {OpaqueCount(plain)}, outlined {OpaqueCount(outlined)})");

        var anyRed = false;
        for (var i = 0; i < outlined.Length && !anyRed; i += 4)
            if (outlined[i] > 180 && outlined[i + 1] < 80 && outlined[i + 2] < 80 && outlined[i + 3] > 150)
                anyRed = true;
        Assert.True(anyRed, "the red outline color should appear");
    }

    /// <summary>A drop shadow adds ink and puts it down-and-right of the glyphs.</summary>
    [Fact]
    public void DropShadow_AddsOffsetInk()
    {
        var plain = RenderFrame(gui =>
            gui.DrawText("Ag", size: Size, color: Color.White, centerInRect: false));
        var shadowed = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White, centerInRect: false,
            effects: new TextEffects
            {
                DropShadow = new TextEffects.TextShadow(
                    Color.FromArgb(220, 0, 0, 0), new Vector2(10, 10), 2f),
            }));

        Assert.True(OpaqueCount(shadowed) > OpaqueCount(plain) + 50, "shadow should add coverage");

        // Somewhere in the frame the shadow is opaque where the plain text is not.
        var newInk = false;
        for (var i = 3; i < plain.Length && !newInk; i += 4)
            if (plain[i] < 10 && shadowed[i] > 40)
                newInk = true;
        Assert.True(newInk, "the shadow should paint pixels the plain text does not");
    }

    /// <summary>A horizontal gradient makes the left and right of the text different colors.</summary>
    [Fact]
    public void Gradient_VariesColorAcrossText()
    {
        var px = RenderFrame(gui => gui.DrawText("MMMMMMMMMM", size: 40f, color: Color.White, centerInRect: false,
            effects: new TextEffects
            {
                Gradient = new TextEffects.TextGradient(
                    Color.FromArgb(255, 255, 0, 0), Color.FromArgb(255, 0, 0, 255)),
            }));

        (long r, long b, int count) Sample(int x0, int x1)
        {
            long r = 0, b = 0;
            var c = 0;
            for (var y = 0; y < H; y++)
            for (var x = x0; x < x1; x++)
            {
                var (pr, pb, pa) = At(px, x, y);
                if (pa <= 60) continue;
                r += pr;
                b += pb;
                c++;
            }

            return (r, b, c);
        }

        var left = Sample(6, 46);
        var right = Sample(200, 240);

        Assert.True(left.count > 20 && right.count > 20,
            $"text should cover both sample windows (left {left.count}, right {right.count})");
        Assert.True((double)left.r / left.count > (double)left.b / left.count, "left of the text should be red-dominant");
        Assert.True((double)right.b / right.count > (double)right.r / right.count, "right of the text should be blue-dominant");
    }

    /// <summary>Effects do not change the node's laid-out size.</summary>
    [Fact]
    public void Effects_DoNotChangeNodeSize()
    {
        LayoutNode? plainNode = null;
        LayoutNode? fxNode = null;

        RenderFrame(gui => plainNode = gui.DrawText("Hello", size: Size, color: Color.White));
        RenderFrame(gui => fxNode = gui.DrawText("Hello", size: Size, color: Color.White, effects: new TextEffects
        {
            Outline = new TextEffects.TextOutline(Color.White, 3f),
            DropShadow = new TextEffects.TextShadow(Color.Black, new Vector2(6, 6), 4f),
        }));

        Assert.NotNull(plainNode);
        Assert.NotNull(fxNode);
        Assert.Equal(plainNode!.Rect.W, fxNode!.Rect.W, 1);
        Assert.Equal(plainNode.Rect.H, fxNode.Rect.H, 1);
    }
}

namespace Guinevere.Tests;

/// <summary>
/// Pixel-level tests for <see cref="TextEffects"/> on <c>Gui.DrawText</c>.
/// The <c>ui --example text</c> CLI verb renders the same effects for eyeballing.
/// </summary>
public class TextEffectsTests
{
    const int W = 400;
    const int H = 140;
    const float Size = 56f;

    static readonly Font Face = Font.FromFamilyName("sans-serif", Size);

    static byte[] RenderFrame(Action<Gui> draw)
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
        return [.. pixmap.GetPixelSpan()];
    }

    static (byte R, byte B, byte A) At(byte[] px, int x, int y)
    {
        var i = ((y * W) + x) * 4;
        return (px[i], px[i + 2], px[i + 3]);
    }

    static int OpaqueCount(byte[] px)
    {
        var n = 0;
        for (var i = 3; i < px.Length; i += 4)
        {
            if (px[i] > 20)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Empty effects render the same as no effects (the refactor is transparent).</summary>
    [Fact]
    public void EmptyEffectsMatchPlainText()
    {
        var plain = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White));
        var empty = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White, effects: new TextEffects()));

        Assert.Equal(plain, empty);
    }

    /// <summary>An outline puts extra ink around the glyphs.</summary>
    [Fact]
    public void OutlineAddsInkAroundGlyphs()
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
        {
            if (outlined[i] > 180 && outlined[i + 1] < 80 && outlined[i + 2] < 80 && outlined[i + 3] > 150)
            {
                anyRed = true;
            }
        }

        Assert.True(anyRed, "the red outline color should appear");
    }

    /// <summary>A drop shadow adds ink and puts it down-and-right of the glyphs.</summary>
    [Fact]
    public void DropShadowAddsOffsetInk()
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
        {
            if (plain[i] < 10 && shadowed[i] > 40)
            {
                newInk = true;
            }
        }

        Assert.True(newInk, "the shadow should paint pixels the plain text does not");
    }

    /// <summary>An inner shadow changes glyph pixels while remaining clipped to their coverage.</summary>
    [Fact]
    public void InnerShadowChangesGlyphPixels()
    {
        var plain = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White));
        var inner = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White,
            effects: new TextEffects
            {
                InnerShadow = new TextEffects.TextShadow(
                    Color.FromArgb(255, 255, 0, 0), new Vector2(4, 2), 2f),
            }));

        Assert.False(plain.SequenceEqual(inner), "the inner shadow should alter covered text pixels");
        Assert.Equal(OpaqueCount(plain), OpaqueCount(inner));
    }

    /// <summary>An explicit empty effect value suppresses effects inherited from the current scope.</summary>
    [Fact]
    public void ExplicitEmptyEffectsOverrideInheritedEffects()
    {
        var plain = RenderFrame(gui => gui.DrawText("Ag", size: Size, color: Color.White));
        var inherited = new TextEffects
        {
            DropShadow = new TextEffects.TextShadow(
                Color.FromArgb(255, 255, 0, 0), new Vector2(20, 12), 3f),
        };
        var overridden = RenderFrame(gui =>
        {
            gui.SetTextEffects(inherited);
            gui.DrawText("Ag", size: Size, color: Color.White, effects: new TextEffects());
        });

        Assert.Equal(plain, overridden);
    }

    /// <summary>Glyphs inherit text effects from their current scope.</summary>
    [Fact]
    public void DrawGlyphInheritsTextEffects()
    {
        var effects = new TextEffects
        {
            DropShadow = new TextEffects.TextShadow(
                Color.FromArgb(255, 255, 0, 0), new Vector2(12, 8), 2f),
        };
        var glyph = RenderFrame(gui =>
        {
            gui.SetTextEffects(effects);
            gui.DrawGlyph('A', size: Size, color: Color.White, font: Face, centerInRect: false);
        });
        var text = RenderFrame(gui =>
        {
            gui.SetTextEffects(effects);
            gui.DrawText("A", size: Size, color: Color.White, font: Face, centerInRect: false);
        });

        Assert.Equal(text, glyph);
    }

    /// <summary>A horizontal gradient makes the left and right of the text different colors.</summary>
    [Fact]
    public void GradientVariesColorAcrossText()
    {
        var px = RenderFrame(gui => gui.DrawText("MMMMMMMMMM", size: 40f, color: Color.White, centerInRect: false,
            effects: new TextEffects
            {
                Gradient = new TextEffects.TextGradient(
                    Color.FromArgb(255, 255, 0, 0), Color.FromArgb(255, 0, 0, 255)),
            }));

        (long r, long b, int count) Example(int x0, int x1)
        {
            long r = 0, b = 0;
            var c = 0;
            for (var y = 0; y < H; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    var (pr, pb, pa) = At(px, x, y);
                    if (pa <= 60)
                    {
                        continue;
                    }

                    r += pr;
                    b += pb;
                    c++;
                }
            }

            return (r, b, c);
        }

        var left = Example(6, 46);
        var right = Example(200, 240);

        Assert.True(left.count > 20 && right.count > 20,
            $"text should cover both example windows (left {left.count}, right {right.count})");
        Assert.True((double)left.r / left.count > (double)left.b / left.count, "left of the text should be red-dominant");
        Assert.True((double)right.b / right.count > (double)right.r / right.count, "right of the text should be blue-dominant");
    }

    /// <summary>Effects do not change the node's laid-out size.</summary>
    [Fact]
    public void EffectsDoNotChangeNodeSize()
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
        Assert.Equal(plainNode.Rect.W, fxNode.Rect.W, 1);
        Assert.Equal(plainNode.Rect.H, fxNode.Rect.H, 1);
    }
}

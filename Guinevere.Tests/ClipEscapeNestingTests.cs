namespace Guinevere.Tests;

/// <summary>
/// A node that escapes ancestor clips (a dialog or popup floating over a dock panel) must not also
/// make its own descendants escape clipping. The escape flag used to be read with
/// <c>LayoutNodeScope.Get</c>, which cascades like every other scope value, so a scroll area nested
/// inside a dialog inherited "escapes everything" too and its own rows painted straight past its
/// bounds instead of being clipped to it.
/// </summary>
public class ClipEscapeNestingTests
{
    const int Surface = 100;

    static SKSurface NewSurface() =>
        SKSurface.Create(new SKImageInfo(Surface, Surface, SKColorType.Rgba8888, SKAlphaType.Unpremul));

    static void RunFrame(SKSurface surface, Action<Gui> draw)
    {
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
        canvas.Flush();
    }

    static (byte R, byte G, byte B, byte A) PixelAt(SKSurface surface, int x, int y)
    {
        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        var span = pixmap.GetPixelSpan();
        var i = ((y * Surface) + x) * 4;
        return (span[i], span[i + 1], span[i + 2], span[i + 3]);
    }

    [Fact]
    public void AScrollContainerNestedInsideAnEscapingNodeStillClipsItsOwnContent()
    {
        using var surface = NewSurface();

        RunFrame(surface, gui =>
        {
            using (gui.Node(60, 60, "escaping").AbsoluteScreen(20, 20).Enter())
            {
                gui.SetEscapesAncestorClips();

                using (gui.Node(40, 20, "scrollArea").Enter())
                {
                    gui.ScrollY();

                    // A fill far taller than the 20px scroll viewport it sits in.
                    using (gui.Node(40, 100).Enter())
                        if (gui.Pass == Pass.Pass2Render)
                            gui.DrawBackgroundRect(Color.FromArgb(255, 255, 0, 0));
                }
            }
        });

        // (30, 30): inside the 20px-tall scroll viewport (which starts at screen y=20) -- filled red.
        Assert.Equal((255, 0, 0, 255), PixelAt(surface, 30, 30));

        // (30, 70): 30px below the viewport's bottom edge, still inside the fill's own 100px height --
        // the scroll container's own clip must hide it despite its ancestor escaping clips above it.
        Assert.Equal((byte)0, PixelAt(surface, 30, 70).A);
    }
}

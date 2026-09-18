namespace Guinevere.Tests;

/// <summary>
/// Repro for the AtomicMassa Studio-shell "flex siblings" bug: a node drawn after a scroll /
/// <see cref="Gui.ClipContent"/> container vanished. The container's clip was pushed onto the
/// canvas in <see cref="Gui.Render"/> and never restored, so it leaked onto every later node in
/// the flat z-ordered draw list.
/// </summary>
public class ImmediateModeSiblingTests
{
    const int Size = 120;

    static byte[] RenderFrame(Action<Gui> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        var input = Substitute.For<IInputHandler>();
        input.MousePosition.Returns(new Vector2(-1, -1));
        input.PrevMousePosition.Returns(new Vector2(-1, -1));

        var gui = new Gui { Input = input };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
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

    static (byte R, byte G, byte B) At(byte[] px, int x, int y)
    {
        var i = ((y * Size) + x) * 4;
        return (px[i], px[i + 1], px[i + 2]);
    }

    /// <summary>
    /// A vertical stack: a scrolling container, then a plain footer row. The footer is well
    /// outside the scroll container's bounds and must still paint.
    /// </summary>
    [Fact]
    public void RowAfterScrollContainer_StillRenders()
    {
        var px = RenderFrame(g =>
        {
            using (g.Node().Expand().Direction(Axis.Vertical).Gap(4).Enter())
            {
                using (g.Node(-1, 40).ExpandWidth().Direction(Axis.Vertical).Enter())
                {
                    g.ScrollY();
                    for (var i = 0; i < 8; i++)
                        using (g.Node(-1, 12).ExpandWidth().Enter())
                            g.DrawRect(g.CurrentNode.Rect, Color.FromArgb(255, 40, 40, 40));
                }

                using (g.Node(-1, 40).ExpandWidth().Enter())
                    g.DrawRect(g.CurrentNode.Rect, Color.FromArgb(255, 0, 200, 0)); // footer
            }
        });

        // footer band spans roughly y=44..84 (40px scroll area + 4px gap); it must paint green
        var greenRows = Enumerable.Range(30, 60).Count(y =>
        {
            var (r, gg, b) = At(px, Size / 2, y);
            return gg > 150 && r < 80 && b < 80;
        });
        Assert.True(greenRows > 20, $"footer after a scroll container did not render (green rows: {greenRows})");
    }

    /// <summary>The scroll container itself still clips its overflowing children.</summary>
    [Fact]
    public void ScrollContainer_StillClipsItsOwnContent()
    {
        var px = RenderFrame(g =>
        {
            using (g.Node().Expand().Direction(Axis.Vertical).Enter())
            {
                using (g.Node(-1, 40).ExpandWidth().Direction(Axis.Vertical).Enter())
                {
                    g.ScrollY();
                    for (var i = 0; i < 30; i++)
                        using (g.Node(-1, 12).ExpandWidth().Enter())
                            g.DrawRect(g.CurrentNode.Rect, Color.FromArgb(255, 200, 0, 0));
                }
            }
        });

        // content past the 40px viewport must be clipped away (still black)
        Assert.Equal((byte)0, At(px, Size / 2, 60).R);
    }
}

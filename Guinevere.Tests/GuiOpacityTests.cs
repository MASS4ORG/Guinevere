namespace Guinevere.Tests;

/// <summary>Tests for <see cref="Gui.SetOpacity"/> group compositing.</summary>
public class GuiOpacityTests
{
    const int Size = 40;

    static byte[] Render(Action<Gui> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        surface.Canvas.Clear(SKColors.Transparent);
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        draw(gui);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        return [.. pixmap.GetPixelSpan()];
    }

    static byte Alpha(byte[] px, int x, int y) => px[(((y * Size) + x) * 4) + 3];

    static void Filled(Gui gui, float opacity, Action? children = null)
    {
        using (gui.Node(Size, Size).Enter())
        {
            gui.SetOpacity(opacity);
            gui.DrawBackgroundRect(Color.Red);
            children?.Invoke();
        }
    }

    /// <summary>A fully transparent node hides its whole subtree, including opaque children.</summary>
    [Fact]
    public void ZeroOpacity_HidesTheSubtree() =>
        Assert.Equal(0, Alpha(Render(g => Filled(g, 0f, () => Filled(g, 1f))), 20, 20));

    /// <summary>Nested groups multiply; an opaque child inside a group does not stack its alpha.</summary>
    [Fact]
    public void NestedGroups_Multiply()
    {
        Assert.InRange(Alpha(Render(g => Filled(g, 0.5f, () => Filled(g, 0.5f))), 20, 20), 120, 135);
        Assert.InRange(Alpha(Render(g => Filled(g, 0.5f, () => Filled(g, 1f))), 20, 20), 120, 135);
        Assert.Equal(255, Alpha(Render(g => Filled(g, 1f)), 20, 20));
    }

    /// <summary>Values outside 0..1 are clamped.</summary>
    [Fact]
    public void Opacity_IsClamped() =>
        Assert.Equal(255, Alpha(Render(g => Filled(g, 3f)), 20, 20));
}

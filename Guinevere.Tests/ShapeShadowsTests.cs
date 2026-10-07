namespace Guinevere.Tests;

/// <summary>Tests for the CSS-semantics shadows of <see cref="Shape"/>.</summary>
public class ShapeShadowsTests
{
    const int Size = 80;

    static byte[] Render(Shape shape)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        surface.Canvas.Clear(SKColors.Transparent);
        shape.Render(new Gui(), null!, surface.Canvas);
        using var snapshot = surface.Snapshot();
        using var pixmap = snapshot.PeekPixels();
        return [.. pixmap.GetPixelSpan()];
    }

    static (byte R, byte G, byte B, byte A) At(byte[] px, int x, int y)
    {
        var i = ((y * Size) + x) * 4;
        return (px[i], px[i + 1], px[i + 2], px[i + 3]);
    }

    static Shape Box(Color? fill = null) => Shape.Rect(20, 20, 60, 60).SolidColor(fill ?? Color.Transparent);

    /// <summary>An inset offset of (0, 10) shades the top edge, like CSS <c>inset 0 10px</c>, not the rest.</summary>
    [Fact]
    public void InnerShadow_ShadesTheEdgeAwayFromTheOffset()
    {
        var px = Render(Box(Color.White).InnerShadow(Color.Black, new Vector2(0, 10)));

        Assert.Equal((0, 0, 0, 255), At(px, 40, 24));
        Assert.Equal((255, 255, 255, 255), At(px, 40, 50));
        Assert.Equal((byte)0, At(px, 40, 10).A);
    }

    /// <summary>A positive inset spread grows the shadow inward on every side.</summary>
    [Fact]
    public void InnerShadow_SpreadGrowsInward()
    {
        var px = Render(Box(Color.White).InnerShadow(Color.Black, 0, 6));

        Assert.Equal((0, 0, 0, 255), At(px, 23, 40));
        Assert.Equal((0, 0, 0, 255), At(px, 57, 40));
        Assert.Equal((255, 255, 255, 255), At(px, 40, 40));
    }

    /// <summary>Outer shadows are cast by the shifted, spread shape and never paint under the shape.</summary>
    [Fact]
    public void OuterShadow_StaysOutsideTheShape()
    {
        var shifted = Render(Box().OuterShadow(Color.Black, new Vector2(8, 8)));
        var spread = Render(Box().OuterShadow(Color.Black, Vector2.Zero, 0, 6));
        var shrunk = Render(Box().OuterShadow(Color.Black, new Vector2(8, 0), 0, -4));

        Assert.Equal((0, 0, 0, 255), At(shifted, 64, 64));
        Assert.Equal((byte)0, At(shifted, 40, 40).A);
        Assert.Equal((0, 0, 0, 255), At(spread, 17, 40));
        Assert.Equal((byte)0, At(spread, 10, 40).A);
        Assert.Equal((0, 0, 0, 255), At(shrunk, 62, 40));
        Assert.Equal((byte)0, At(shrunk, 62, 22).A);
    }

    /// <summary>Blur softens the edge; the first shadow added is drawn on top.</summary>
    [Fact]
    public void Shadows_BlurAndStackFirstOnTop()
    {
        var blurred = Render(Box().OuterShadow(Color.Black, Vector2.Zero, 10, 2));
        var stacked = Render(Box()
            .OuterShadow(Color.Red, new Vector2(10, 0))
            .OuterShadow(Color.Blue, new Vector2(10, 0)));

        Assert.InRange(At(blurred, 62, 40).A, 1, 254);
        Assert.Equal((255, 0, 0, 255), At(stacked, 65, 40));
    }

    /// <summary>Copies and moves keep their shadows, which follow the moved path.</summary>
    [Fact]
    public void CopyAndMove_KeepShadows()
    {
        var shape = Box().OuterShadow(Color.Black, new Vector2(8, 8));

        Assert.Equal((0, 0, 0, 255), At(Render(shape.Copy()), 64, 64));
        Assert.Equal((0, 0, 0, 255), At(Render(shape.Move(-10, -10)), 54, 54));
        Assert.Equal((0, 0, 0, 255), At(Render(shape.MoveX(-10)), 54, 64));
        Assert.Equal((0, 0, 0, 255), At(Render(shape.MoveY(-10)), 64, 54));
    }

    /// <summary>An L-shaped path takes the general path-operation route with the same semantics.</summary>
    [Fact]
    public void GeneralPaths_FollowTheSameSemantics()
    {
        static Shape L(Color fill) => (Shape.Rect(20, 20, 60, 40) + Shape.Rect(20, 20, 40, 60)).SolidColor(fill);

        var inset = Render(L(Color.White).InnerShadow(Color.Black, new Vector2(0, 10), 0, 2));
        var outer = Render(L(Color.Transparent).OuterShadow(Color.Black, new Vector2(8, 8), 0, 2));
        var swallowed = Render(L(Color.White).InnerShadow(Color.Black, 0, 40));

        Assert.Equal((0, 0, 0, 255), At(inset, 30, 24));
        Assert.Equal((255, 255, 255, 255), At(inset, 30, 50));
        Assert.Equal((0, 0, 0, 255), At(outer, 64, 46));
        Assert.Equal((byte)0, At(outer, 30, 30).A);
        Assert.Equal((0, 0, 0, 255), At(swallowed, 30, 30));
    }

    /// <summary>Ovals and rounded rectangles take the fast path and keep their curved shadow edge.</summary>
    [Fact]
    public void OvalsAndRoundRects_UseTheBoxPath()
    {
        var oval = Render(Shape.Circle(20, new Vector2(40, 40)).SolidColor(Color.Transparent)
            .OuterShadow(Color.Black, Vector2.Zero, 0, 4));
        var rounded = Render(Shape.RoundRect(20, 20, 60, 60, 10).SolidColor(Color.Transparent)
            .OuterShadow(Color.Black, Vector2.Zero, 0, 4));

        Assert.Equal((0, 0, 0, 255), At(oval, 40, 18));
        Assert.Equal((byte)0, At(oval, 18, 18).A);
        Assert.Equal((0, 0, 0, 255), At(rounded, 40, 17));
        Assert.Equal((byte)0, At(rounded, 17, 17).A);
    }

    /// <summary>Under an opaque fill the outer shadow skips its clip, with the same visible result.</summary>
    [Fact]
    public void OuterShadow_UnderOpaqueFill()
    {
        var solid = Render(Box(Color.White).OuterShadow(Color.Black, new Vector2(8, 8)));
        var gradient = Box(Color.Black);
        gradient.LinearGradientColor(Color.White, Color.White);
        gradient.OpaqueFill = true;
        var shaded = Render(gradient.OuterShadow(Color.Black, new Vector2(8, 8)));

        Assert.Equal((0, 0, 0, 255), At(solid, 64, 64));
        Assert.Equal((255, 255, 255, 255), At(solid, 50, 50));
        Assert.Equal((0, 0, 0, 255), At(shaded, 64, 64));
        Assert.Equal((255, 255, 255, 255), At(shaded, 50, 50));
    }

    /// <summary>A spread that swallows the whole hole shades the full shape.</summary>
    [Fact]
    public void InnerShadow_HugeSpreadFillsTheShape() =>
        Assert.Equal((0, 0, 0, 255), At(Render(Box(Color.White).InnerShadow(Color.Black, 0, 40)), 40, 40));
}

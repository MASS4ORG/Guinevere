namespace Guinevere.Tests;

/// <summary>Tests for the painted areas drawables report through <see cref="IInkBounds"/>.</summary>
public class InkBoundsTests
{
    static readonly Gui Gui = new() { Input = Substitute.For<IInputHandler>() };
    static readonly LayoutNode Node = LayoutNode.CreateRoot(Gui, 100, 100);

    static SKRect? Bounds(object drawable) => ((IInkBounds)drawable).InkBounds(Node);

    /// <summary>Text covers its measured glyphs; empty text covers nothing; filtered paints are unbounded.</summary>
    [Fact]
    public void Text_CoversItsGlyphs()
    {
        var glyphs = Bounds(new Text("Hi", new Vector2(10, 50), new SKFont(SKTypeface.Default, 20)))!.Value;

        Assert.InRange(glyphs.Left, 8, 12);
        Assert.InRange(glyphs.Bottom, 45, 56);
        Assert.True(glyphs.Width > 5);
        Assert.True(Bounds(new Text("Hi"))!.Value.Width > 0);
        Assert.Equal(SKRect.Empty, Bounds(new Text("")));
        Assert.Null(Bounds(new Text("Hi", paint: new SKPaint { ImageFilter = SKImageFilter.CreateBlur(2, 2) })));
    }

    /// <summary>Images, nine-slices and deferred rects cover their destination; strokes add half their width.</summary>
    [Fact]
    public void Rectangles_CoverTheirDestination()
    {
        using var bitmap = new SKBitmap(4, 4);
        using var image = SKImage.FromBitmap(bitmap);
        var stroke = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 4 };

        Assert.Equal(new SKRect(1, 2, 4, 6), Bounds(new ImageDrawable(image, new Rect(1, 2, 3, 4))));
        Assert.Equal(new SKRect(1, 2, 4, 6), Bounds(new NineSliceDrawable(image, new Rect(1, 2, 3, 4), new Insets(1))));
        Assert.Equal(new SKRect(Node.Rect.X, Node.Rect.Y, Node.Rect.X + Node.Rect.W, Node.Rect.Y + Node.Rect.H),
            Bounds(DeferShape.DrawRectFilled(Color.Red, 0)));
        Assert.Equal(new SKRect(-3, -3, 13, 13), Ink.Painted(new SKRect(0, 0, 10, 10), stroke));
        Assert.Null(Ink.Painted(new SKRect(), new SKPaint { MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2) }));
    }

    /// <summary>Shapes add their outer shadows' reach; inset shadows stay inside.</summary>
    [Fact]
    public void Shape_IncludesOuterShadows()
    {
        var shape = Shape.Rect(10, 10, 20, 20).OuterShadow(Color.Black, 4, 2).InnerShadow(Color.Black, 30);

        Assert.Equal(new SKRect(2, 2, 28, 28), Bounds(shape));
        Assert.Equal(new SKRect(10, 10, 20, 20), Bounds(Shape.Rect(10, 10, 20, 20)));
    }

    /// <summary>
    /// A draw list unions its drawables and ignores clips; a custom entry or a picture icon is bounded as well as it
    /// can be, and anything unknown makes the whole list unbounded.
    /// </summary>
    [Fact]
    public void DrawList_UnionsAndFallsBack()
    {
        var list = new DrawList();
        list.Add(Shape.Rect(0, 0, 10, 10));
        list.AddClip(new Rect(0, 0, 5, 5));
        list.Add(Shape.Rect(20, 20, 30, 30));

        Assert.Equal(new SKRect(0, 0, 30, 30), list.InkBounds(Node));
        list.Add(Substitute.For<IDrawListEntry>());
        Assert.Null(list.InkBounds(Node));
    }

    /// <summary>A picture icon covers its destination square.</summary>
    [Fact]
    public void PictureIcon_CoversItsSquare()
    {
        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(new SKRect(0, 0, 10, 10)).DrawRect(0, 0, 10, 10, new SKPaint());
        var icon = Icon.FromPicture(recorder.EndRecording());
        using var surface = SKSurface.Create(new SKImageInfo(40, 40));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        LayoutNode? node = null;

        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        gui.Icon(icon, 20);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        node = gui.Icon(icon, 20);

        Assert.Equal(new SKRect(0, 0, 20, 20), node.DrawList.InkBounds(node));
    }
}

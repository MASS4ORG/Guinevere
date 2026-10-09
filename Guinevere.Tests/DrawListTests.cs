namespace Guinevere.Tests;

/// <summary>Checks reusable command storage, replay side effects, clips and direct primitive ink bounds.</summary>
public class DrawListTests
{
    [Fact]
    public void ShapeClipReplayDoesNotMoveCallerPathOrCanvasMatrix()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 80));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        var node = LayoutNode.CreateRoot(gui, 80, 80);
        var shape = Shape.Circle(10);
        var originalBounds = shape.Path.Bounds;
        node.DrawList.AddClip(shape, new Vector2(20, 20));
        for (var i = 0; i < 2; i++)
        {
            var save = surface.Canvas.Save();
            surface.Canvas.Translate(5, 7);
            var matrix = surface.Canvas.TotalMatrix;
            node.DrawList.Render(gui, node, surface.Canvas);
            Assert.Equal(matrix, surface.Canvas.TotalMatrix);
            Assert.Equal(new SKRectI(15, 17, 35, 37), surface.Canvas.DeviceClipBounds);
            surface.Canvas.RestoreToCount(save);
            Assert.Equal(originalBounds, shape.Path.Bounds);
        }
    }

    [Fact]
    public void ReplayKeepsCustomTransformsAndMutableShadersInOrder()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 80));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        var node = LayoutNode.CreateRoot(gui, 80, 80);
        var list = node.DrawList;
        var before = Substitute.For<IDrawListEntry>();
        before.When(entry => entry.Execute(gui, node, surface.Canvas))
            .Do(_ => surface.Canvas.Translate(10, 0));
        var shape = Shape.Rect(0, 0, 20, 20).SolidColor(Color.White);
        using var shader = SKShader.CreateColor(SKColors.Blue);
        shape.Paint!.Shader = shader;
        list.Add(shape);
        list.Prepend(before);
        list.Add(new PrimitiveCommand(PrimitiveKind.Rect, new Rect(0, 30, 20, 20), SKColors.Red));
        var after = Substitute.For<IDrawListEntry>();
        list.Add(after);
        for (var i = 0; i < 2; i++)
        {
            surface.Canvas.Clear(SKColors.Transparent);
            var save = surface.Canvas.Save();
            list.Render(gui, node, surface.Canvas);
            surface.Canvas.RestoreToCount(save);
            using var image = surface.Snapshot();
            using var pixels = SKBitmap.FromImage(image);
            Assert.Equal(SKColors.Blue, pixels.GetPixel(15, 10));
            Assert.Equal(SKColors.Red, pixels.GetPixel(15, 40));
            Assert.Equal(0, pixels.GetPixel(5, 40).Alpha);
        }
        before.Received(2).Execute(gui, node, surface.Canvas);
        after.Received(2).Execute(gui, node, surface.Canvas);
        Assert.Equal(SKColors.White, shape.Paint.Color);
        Assert.Same(shader, shape.Paint.Shader);
    }

    [Fact]
    public void PrimitiveInkBoundsIncludeStrokesAndLineWidths()
    {
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        var node = LayoutNode.CreateRoot(gui, 80, 80);
        var list = node.DrawList;
        list.Add(new PrimitiveCommand(PrimitiveKind.Rect, new Rect(10, 10, 20, 20), SKColors.Red,
            Thickness: 4, Stroke: true));
        Assert.Equal(new SKRect(7, 7, 33, 33), list.InkBounds(node));
        list.Clear();
        list.Add(new PrimitiveCommand(PrimitiveKind.Circle, default, SKColors.Red, 10, A: new Vector2(20, 20)));
        Assert.Equal(new SKRect(10, 10, 30, 30), list.InkBounds(node));
        list.Clear();
        list.Add(new PrimitiveCommand(PrimitiveKind.Line, default, SKColors.Red, Thickness: 4,
            A: new Vector2(30, 10), B: new Vector2(10, 30)));
        Assert.Equal(new SKRect(7, 7, 33, 33), list.InkBounds(node));
        list.Clear();
        list.Add(new PrimitiveCommand(PrimitiveKind.Triangle, default, SKColors.Red, A: new Vector2(20, 10),
            B: new Vector2(10, 30), C: new Vector2(40, 20)));
        Assert.Equal(new SKRect(10, 10, 40, 30), list.InkBounds(node));
    }

    [Fact]
    public void PrimitivePrependAndClearKeepIndicesValid()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 80));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        var node = LayoutNode.CreateRoot(gui, 80, 80);
        var list = node.DrawList;
        list.EnsureCapacity(3);
        for (var i = 0; i < 2; i++)
        {
            list.Clear();
            list.Add(new PrimitiveCommand(PrimitiveKind.Rect, new Rect(0, 0, 30, 30), SKColors.Red));
            list.Add(new PrimitiveCommand(PrimitiveKind.Rect, new Rect(0, 0, 60, 60), SKColors.Blue), prepend: true);
            list.AddClip(new Rect(0, 0, 80, 80));
            Assert.Equal(3, list.Count);
            Assert.Equal(2, list.PrimitiveCount);
            var save = surface.Canvas.Save();
            list.Render(gui, node, surface.Canvas);
            surface.Canvas.RestoreToCount(save);
            using var image = surface.Snapshot();
            using var pixels = SKBitmap.FromImage(image);
            Assert.Equal(SKColors.Red, pixels.GetPixel(15, 15));
            Assert.Equal(SKColors.Blue, pixels.GetPixel(45, 45));
        }
    }
}

namespace Guinevere.Tests;

/// <summary>Checks direct commands, fluent shape mutations, drawing order and command allocation.</summary>
public class GuiDrawShapesTests
{
    static Gui Begin(SKCanvas canvas)
    {
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(canvas);
        return gui;
    }

    static SKBitmap Render(Action<Gui> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 80));
        surface.Canvas.Clear(SKColors.Transparent);
        var gui = Begin(surface.Canvas);
        draw(gui);
        Assert.Equal(0, gui.RootNode!.DrawList.Count);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        draw(gui);
        gui.Render();
        gui.EndFrame();
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    /// <summary>Direct backgrounds apply alpha once behind previously queued content.</summary>
    [Fact]
    public void BackgroundIsPrependedExactlyOnce()
    {
        using var pixels = Render(gui =>
        {
            gui.DrawRect(new Rect(20, 20, 20, 20), Color.Blue);
            gui.DrawBackgroundRect(new Color(0xff000080));
            if (gui.Pass == Pass.Pass2Render) Assert.Equal(2, gui.RootNode!.DrawList.Count);
        });
        Assert.Equal(128, pixels.GetPixel(10, 10).Alpha);
        Assert.Equal(SKColors.Blue, pixels.GetPixel(30, 30));
    }

    /// <summary>Each vertex contributes its own color to triangle pixels.</summary>
    [Fact]
    public void TriangleInterpolatesAllThreeVertexColors()
    {
        using var pixels = Render(gui => gui.DrawTriangle(new Vector2(5, 5), new Vector2(75, 5),
            new Vector2(5, 75), Color.Red, Color.Lime, Color.Blue));
        Assert.True(pixels.GetPixel(8, 8).Red > 220);
        Assert.True(pixels.GetPixel(68, 8).Green > 210);
        Assert.True(pixels.GetPixel(8, 68).Blue > 210);
        var middle = pixels.GetPixel(28, 28);
        Assert.InRange(middle.Red, 75, 95);
        Assert.InRange(middle.Green, 75, 95);
        Assert.InRange(middle.Blue, 75, 95);
    }

    /// <summary>Missing vertex colors use the first color.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TriangleOmittedColorsUseFirstColor(bool thirdOnly)
    {
        using var pixels = Render(gui => gui.DrawTriangleFilled(new Vector2(5, 5), new Vector2(75, 5),
            new Vector2(5, 75), Color.Red, colorC: thirdOnly ? Color.Blue : null));
        Assert.True(pixels.GetPixel(68, 8).Red > 210);
        Assert.InRange(pixels.GetPixel(8, 68).Blue, thirdOnly ? 210 : 0, thirdOnly ? 255 : 0);
    }

    /// <summary>Primitive commands respect node ordering, clipping and group opacity.</summary>
    [Fact]
    public void DirectCommandsKeepZOrderClipAndOpacity()
    {
        using var pixels = Render(gui =>
        {
            using (gui.Node(40, 40, "top").Enter())
            {
                gui.SetZIndex(5);
                gui.SetOpacity(0.5f);
                gui.ClipContent();
                gui.DrawRect(new Rect(0, 0, 80, 80), Color.Blue);
            }
            using (gui.Node(80, 80, "bottom").Absolute(0, 0).Enter())
                gui.DrawBackgroundRect(Color.Red);
        });
        var overlap = pixels.GetPixel(20, 20);
        Assert.InRange(overlap.Blue, 120, 135);
        Assert.InRange(overlap.Red, 120, 135);
        Assert.Equal(SKColors.Red, pixels.GetPixel(60, 60));
    }

    /// <summary>Rounded rectangles retain only the requested rounded corners.</summary>
    [Theory]
    [InlineData(Corner.All)]
    [InlineData(Corner.TopLeft)]
    [InlineData(Corner.None)]
    public void RoundedRectKeepsSelectedCorners(Corner corners)
    {
        using var pixels = Render(gui => gui.DrawRect(new Rect(10, 10, 50, 50), Color.Red, 15, corners));
        Assert.Equal(corners.HasFlag(Corner.TopLeft) ? 0 : 255, pixels.GetPixel(10, 10).Alpha);
        Assert.Equal(corners.HasFlag(Corner.BottomRight) ? 0 : 255, pixels.GetPixel(59, 59).Alpha);
        Assert.Equal(SKColors.Red, pixels.GetPixel(30, 30));
    }

    /// <summary>Geometry-only rectangle calls select live fluent shapes without overload ambiguity.</summary>
    [Fact]
    public void RectangleWithoutColorReturnsMutableShape()
    {
        using var pixels = Render(gui =>
        {
            gui.DrawRect(new Rect(5, 5, 20, 20)).SolidColor(Color.Blue);
            gui.DrawRect(new Rect(35, 5, 20, 20), radius: 3).SolidColor(Color.Lime);
            if (gui.Pass == Pass.Pass2Render) Assert.Equal(0, gui.RootNode!.DrawList.PrimitiveCount);
        });
        Assert.Equal(SKColors.Blue, pixels.GetPixel(15, 15));
        Assert.Equal(SKColors.Lime, pixels.GetPixel(45, 15));
    }

    /// <summary>Fluent no-color and direct null-color backgrounds each queue one white background.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackgroundOverloadsUseWhiteAndQueueOnce(bool fluent)
    {
        using var pixels = Render(gui =>
        {
            if (fluent)
            {
                var background = gui.DrawBackgroundRect();
                Assert.Equal(SKColors.White, background.Paint!.Color);
            }
            else
                gui.DrawBackgroundRect((Color?)null);
            if (gui.Pass == Pass.Pass2Render)
            {
                Assert.Equal(1, gui.RootNode!.DrawList.Count);
                Assert.Equal(fluent ? 0 : 1, gui.RootNode.DrawList.PrimitiveCount);
            }
        });
        Assert.Equal(SKColors.White, pixels.GetPixel(40, 40));
    }

    static void DirectCalls(Gui gui)
    {
        gui.DrawRect(new Rect(10, 10, 20, 20), Color.Red);
        gui.DrawRect(new Vector2(10, 10), new Vector2(20, 20), Color.Red);
        gui.DrawRect(new Rect(10, 10, 20, 20), (Color?)null);
        gui.DrawRectFilled(10, 10, 30, 30, Color.Red);
        gui.DrawRectBorder(new Rect(10, 10, 20, 20), Color.Blue, 2);
        gui.DrawRectBorder(new Vector2(10, 10), new Vector2(20, 20), Color.Blue, 2);
        gui.DrawCircle(new Vector2(40, 40), 10, Color.Red);
        gui.DrawCircleFilled(new Vector2(40, 40), 10, Color.Red);
        gui.DrawCircleBorder(new Vector2(40, 40), 10, Color.Blue, 2);
        gui.DrawTriangle(new Vector2(1, 1), new Vector2(20, 1), new Vector2(1, 20), Color.Red);
        gui.DrawLine(new Vector2(5, 5), new Vector2(50, 50), Color.White, 0);
        gui.DrawBackgroundRect((Color?)null);
    }

    /// <summary>Warmed direct commands allocate no managed memory in either pass.</summary>
    [Fact]
    public void WarmedDirectRecordingAndBuildAllocateNothing()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 80));
        var gui = Begin(surface.Canvas);
        var list = gui.RootNode!.DrawList;
        list.EnsureCapacity(12);
        for (var i = 0; i < 100; i++) DirectCalls(gui);
        var before = GC.GetAllocatedBytesForCurrentThread();
        DirectCalls(gui);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, list.Count);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        for (var i = 0; i < 100; i++)
        {
            list.Clear();
            DirectCalls(gui);
        }
        list.Clear();
        before = GC.GetAllocatedBytesForCurrentThread();
        DirectCalls(gui);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(12, list.Count);
        Assert.Equal(12, list.PrimitiveCount);
        gui.Render();
        list.Clear();
        Assert.Equal(0, list.PrimitiveCount);
    }

    /// <summary>Scratch paint resets preserve subsequent fill colors and style.</summary>
    [Fact]
    public void BordersAndLinesDoNotChangeFollowingFillPaint()
    {
        using var pixels = Render(gui =>
        {
            gui.DrawRectBorder(new Rect(5, 5, 20, 20), Color.Blue, 4, 4);
            gui.DrawCircleBorder(new Vector2(50, 15), 10, Color.Lime, 4);
            gui.DrawLine(new Vector2(5, 35), new Vector2(65, 35), Color.Blue, 4);
            gui.DrawRectFilled(new Rect(10, 45, 20, 20), null);
            gui.DrawCircle(new Vector2(50, 55), 10, Color.Red);
        });
        Assert.Equal(0, pixels.GetPixel(15, 15).Alpha);
        Assert.Equal(SKColors.Blue, pixels.GetPixel(5, 15));
        Assert.Equal(SKColors.Lime, pixels.GetPixel(40, 15));
        Assert.Equal(SKColors.Blue, pixels.GetPixel(30, 35));
        Assert.Equal(SKColors.Black, pixels.GetPixel(20, 55));
        Assert.Equal(SKColors.Red, pixels.GetPixel(50, 55));
    }

    /// <summary>Fluent shape mutations remain visible until replay.</summary>
    [Fact]
    public void FluentShapesUseMutationsMadeAfterRecording()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 80));
        surface.Canvas.Clear(SKColors.Transparent);
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.SetStage(Pass.Pass1Build);
        gui.BeginFrame(surface.Canvas);
        Build(gui);
        Assert.Equal(0, gui.RootNode!.DrawList.Count);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        Build(gui);
        Assert.Equal(4, gui.RootNode.DrawList.Count);
        Assert.Equal(0, gui.RootNode.DrawList.PrimitiveCount);
        gui.Render();
        using var image = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Blue, pixels.GetPixel(10, 10));
        Assert.Equal(SKColors.Lime, pixels.GetPixel(30, 10));
        Assert.Equal(SKColors.Red, pixels.GetPixel(50, 10));
        Assert.Equal(128, pixels.GetPixel(70, 70).Alpha);
    }

    static void Build(Gui gui)
    {
        var rect = gui.DrawRect(new Vector2(5, 5), new Vector2(10, 10));
        var circle = gui.DrawCircle(new Vector2(30, 10), 5).SolidColor(Color.Red);
        var triangle = gui.DrawTriangle(new Vector2(45, 5), new Vector2(65, 5), new Vector2(45, 25));
        gui.DrawBackgroundRect(2, Corner.Top).SolidColor(new Color(0xffffff80));
        rect.SolidColor(Color.Blue);
        circle.Paint!.Color = SKColors.Lime;
        triangle.SolidColor(Color.Red);
    }

    /// <summary>Translated fluent copies preserve source paths and paints.</summary>
    [Fact]
    public void FluentShapeCopiesEveryLayerBeforeTranslation()
    {
        using var surface = SKSurface.Create(new SKImageInfo(80, 80));
        var gui = new Gui { Input = Substitute.For<IInputHandler>() };
        gui.BeginFrame(surface.Canvas);
        gui.CalculateLayout();
        gui.SetStage(Pass.Pass2Render);
        var original = Shape.Rect(0, 0, 10, 10).SolidColor(Color.Red);
        var extra = Shape.Circle(5, new Vector2(5, 5)).SolidColor(Color.Blue);
        original.AddToLayer(1, extra.Path, extra.Paint!);
        var copy = gui.DrawShape(new Vector2(20, 30), original);
        copy.SolidColor(Color.Lime);
        Assert.Equal(new SKRect(0, 0, 10, 10), original.Path.Bounds);
        Assert.Equal(new SKRect(0, 0, 10, 10), extra.Path.Bounds);
        Assert.Equal(new SKRect(20, 30, 30, 40), copy.Path.Bounds);
        Assert.Equal(new SKRect(20, 30, 30, 40), copy.Layers[1][0].path.Bounds);
        Assert.Equal(SKColors.Red, original.Paint!.Color);
        Assert.Same(gui.CurrentNode, copy.Node);
        gui.DrawShape(Vector2.Zero, original);
        Assert.Equal(2, gui.CurrentNode.DrawList.Count);
    }
}

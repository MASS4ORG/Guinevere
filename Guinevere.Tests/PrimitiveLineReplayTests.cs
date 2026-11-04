namespace Guinevere.Tests;

/// <summary>Checks line replay against independent quadrilateral rendering and allocation limits.</summary>
public class PrimitiveLineReplayTests
{
    /// <summary>Lines preserve reference pixels through clipping, scaling, reflection and rotation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void LinesMatchQuadrilateralPixels(int transform)
    {
        using var expected = SKSurface.Create(new SKImageInfo(180, 120));
        using var actual = SKSurface.Create(new SKImageInfo(180, 120));
        using var renderer = new PrimitiveRenderer();
        foreach (var canvas in new[] { expected.Canvas, actual.Canvas })
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(40, 30);
            switch (transform)
            {
                case 1: canvas.Scale(-1, 1); break;
                case 2: canvas.Scale(.5f, 2); break;
                case 3: canvas.Translate(.25f, .75f); break;
                case 4: canvas.RotateDegrees(17); break;
                case 5: canvas.Skew(.2f, .1f); break;
                case 6: canvas.Scale(100000, 1); break;
            }
            canvas.ClipRoundRect(new SKRoundRect(new SKRect(-20.5f, -10.25f, 110.5f, 65.75f), 7),
                SKClipOperation.Intersect, true);
            using var opacity = new SKPaint { Color = SKColors.White.WithAlpha(170) };
            canvas.SaveLayer(opacity);
        }
        foreach (var thickness in new[] { .25f, 1, 2, 3, 4 })
        {
            foreach (var (a, b) in new (Vector2, Vector2)[]
                     {
                         (new(0, 5), new(75, 5)), (new(75, 15), new(0, 15)),
                         (new(15, 0), new(15, 60)), (new(30, 60), new(30, 0)),
                         (new(0, 30.5f), new(75, 30.5f)), (new(0, 0), new(75, 60)),
                         (new(50, 50), new(50, 50)), (new(0, 5), new(.00001f, 5))
                     })
            {
                var command = new PrimitiveCommand(PrimitiveKind.Line, default, new SKColor(40, 80, 120, 128),
                    Thickness: thickness, A: a, B: b);
                ReferenceLine(expected.Canvas, command);
                renderer.Render(command, actual.Canvas);
            }
        }
        expected.Canvas.Restore();
        actual.Canvas.Restore();
        using var expectedImage = expected.Snapshot();
        using var actualImage = actual.Snapshot();
        using var expectedBitmap = SKBitmap.FromImage(expectedImage);
        using var actualBitmap = SKBitmap.FromImage(actualImage);
        Assert.Equal(expectedBitmap.Pixels, actualBitmap.Pixels);
    }

    static void ReferenceLine(SKCanvas canvas, PrimitiveCommand command)
    {
        var delta = command.B - command.A;
        var half = MathF.Max(.5f, command.Thickness * .5f);
        var normal = delta.Length() > .0001f
            ? new Vector2(-delta.Y, delta.X) / delta.Length() * half
            : new Vector2(half, 0);
        SKPoint[] points = [
            new(command.A.X + normal.X, command.A.Y + normal.Y),
            new(command.B.X + normal.X, command.B.Y + normal.Y),
            new(command.B.X - normal.X, command.B.Y - normal.Y),
            new(command.A.X - normal.X, command.A.Y - normal.Y)
        ];
        using var builder = new SKPathBuilder();
        builder.MoveTo(points[0]);
        foreach (var point in points.AsSpan(1))
        {
            builder.LineTo(point);
        }
        builder.Close();
        using var path = builder.Detach();
        using var paint = new SKPaint { Color = command.Color, IsAntialias = true };
        canvas.DrawPath(path, paint);
    }

    /// <summary>Eligible lines reuse native paint without allocating a managed path per submission.</summary>
    [Fact]
    public void PixelAlignedLinesDoNotAllocatePaths()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 50));
        using var renderer = new PrimitiveRenderer();
        var command = new PrimitiveCommand(PrimitiveKind.Line, default, SKColors.Blue, Thickness: 2,
            A: new Vector2(5, 5), B: new Vector2(75, 5));
        for (var i = 0; i < 100; i++)
        {
            renderer.Render(command, surface.Canvas);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            renderer.Render(command, surface.Canvas);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Picture playback keeps reference antialiasing when its final transform is unknown at recording time.</summary>
    [Fact]
    public void RecordedLinesMatchTransformedReference()
    {
        var command = new PrimitiveCommand(PrimitiveKind.Line, default, SKColors.Blue, Thickness: 2,
            A: new Vector2(5, 5), B: new Vector2(75, 5));
        using var expectedRecorder = new SKPictureRecorder();
        using var actualRecorder = new SKPictureRecorder();
        ReferenceLine(expectedRecorder.BeginRecording(new SKRect(0, 0, 100, 50)), command);
        using var renderer = new PrimitiveRenderer();
        renderer.Render(command, actualRecorder.BeginRecording(new SKRect(0, 0, 100, 50)));
        using var expectedPicture = expectedRecorder.EndRecording();
        using var actualPicture = actualRecorder.EndRecording();
        using var expected = SKSurface.Create(new SKImageInfo(100, 50));
        using var actual = SKSurface.Create(new SKImageInfo(100, 50));
        expected.Canvas.Scale(.7f, 1.3f);
        actual.Canvas.Scale(.7f, 1.3f);
        expected.Canvas.DrawPicture(expectedPicture);
        actual.Canvas.DrawPicture(actualPicture);
        using var expectedImage = expected.Snapshot();
        using var actualImage = actual.Snapshot();
        using var expectedBitmap = SKBitmap.FromImage(expectedImage);
        using var actualBitmap = SKBitmap.FromImage(actualImage);
        Assert.Equal(expectedBitmap.Pixels, actualBitmap.Pixels);
    }
}

namespace Guinevere;

/// <summary>Shares scratch native resources during one replay and releases them when replay ends.</summary>
sealed class PrimitiveRenderer : IDisposable
{
    SKPaint? _paint;
    SKRoundRect? _roundRect;
    SKPathBuilder? _pathBuilder;
    SKPoint[]? _vertices;
    SKColor[]? _colors;

    internal void Render(in PrimitiveCommand command, SKCanvas canvas)
    {
        var paint = _paint ??= new SKPaint();
        paint.Reset();
        paint.Color = command.Color;
        paint.IsAntialias = !command.Stroke;
        paint.Style = command.Stroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill;
        paint.StrokeWidth = command.Thickness;
        switch (command.Kind)
        {
            case PrimitiveKind.Rect:
                Rect(command, canvas, paint);
                break;
            case PrimitiveKind.Circle:
                canvas.DrawCircle(command.A.X, command.A.Y, command.Radius, paint);
                break;
            case PrimitiveKind.Line:
                Line(command, canvas, paint);
                break;
            case PrimitiveKind.Triangle:
                Triangle(command, canvas, paint);
                break;
        }
    }

    void Rect(in PrimitiveCommand command, SKCanvas canvas, SKPaint paint)
    {
        if (ImMath.ApproximatelyEquals(command.Radius, 0f))
        {
            canvas.DrawRect(command.Rect, paint);
            return;
        }
        if (command.Corners == Corner.All)
        {
            canvas.DrawRoundRect(command.Rect, command.Radius, command.Radius, paint);
            return;
        }
        var rounded = _roundRect ??= new SKRoundRect();
        Span<SKPoint> radii =
        [
            CornerRadius(command, Corner.TopLeft),
            CornerRadius(command, Corner.TopRight),
            CornerRadius(command, Corner.BottomRight),
            CornerRadius(command, Corner.BottomLeft),
        ];
        rounded.SetRectRadii(command.Rect, radii);
        canvas.DrawRoundRect(rounded, paint);
    }

    static SKPoint CornerRadius(in PrimitiveCommand command, Corner corner) =>
        (command.Corners & corner) != 0 ? new SKPoint(command.Radius, command.Radius) : SKPoint.Empty;

    void Line(in PrimitiveCommand command, SKCanvas canvas, SKPaint paint)
    {
        var direction = command.B - command.A;
        var length = direction.Length();
        var half = MathF.Max(0.5f, command.Thickness * 0.5f);
        var normal = length > 0.0001f
            ? new Vector2(-direction.Y, direction.X) / length * half
            : new Vector2(half, 0);
        var builder = _pathBuilder ??= new SKPathBuilder();
        builder.Reset();
        builder.MoveTo(command.A.X + normal.X, command.A.Y + normal.Y);
        builder.LineTo(command.B.X + normal.X, command.B.Y + normal.Y);
        builder.LineTo(command.B.X - normal.X, command.B.Y - normal.Y);
        builder.LineTo(command.A.X - normal.X, command.A.Y - normal.Y);
        builder.Close();
        using var path = builder.Detach();
        canvas.DrawPath(path, paint);
    }

    void Triangle(in PrimitiveCommand command, SKCanvas canvas, SKPaint paint)
    {
        if (command.Color == command.ColorB && command.Color == command.ColorC)
        {
            var builder = _pathBuilder ??= new SKPathBuilder();
            builder.Reset();
            builder.MoveTo(command.A.X, command.A.Y);
            builder.LineTo(command.B.X, command.B.Y);
            builder.LineTo(command.C.X, command.C.Y);
            builder.Close();
            using var path = builder.Detach();
            canvas.DrawPath(path, paint);
            return;
        }
        var points = _vertices ??= new SKPoint[3];
        var colors = _colors ??= new SKColor[3];
        points[0] = new SKPoint(command.A.X, command.A.Y);
        points[1] = new SKPoint(command.B.X, command.B.Y);
        points[2] = new SKPoint(command.C.X, command.C.Y);
        colors[0] = command.Color;
        colors[1] = command.ColorB;
        colors[2] = command.ColorC;
        paint.Color = SKColors.White;
        using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, points, colors);
        canvas.DrawVertices(vertices, SKBlendMode.Modulate, paint);
    }

    /// <summary>Releases replay scratch resources without touching caller-owned shapes or paints.</summary>
    public void Dispose()
    {
        _paint?.Dispose();
        _roundRect?.Dispose();
        _pathBuilder?.Dispose();
    }
}

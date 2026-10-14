namespace Guinevere;

static class FillGeometry
{
    internal static SKPath Create(Rect bounds, float fraction, FillOptions options)
    {
        using var path = new SKPathBuilder();
        if (fraction <= 0f || bounds.W <= 0f || bounds.H <= 0f)
        {
            return path.Detach();
        }
        switch (options.Kind)
        {
            case FillKind.Radial: AddRadial(path, bounds, fraction, options); break;
            case FillKind.Segmented: AddSegments(path, bounds, fraction, options); break;
            default: path.AddRect(Linear(bounds, fraction, options.Origin)); break;
        }
        return path.Detach();
    }

    static Rect Linear(Rect bounds, float fraction, FillOrigin origin) => origin switch
    {
        FillOrigin.Right => new(bounds.X + bounds.W * (1f - fraction), bounds.Y, bounds.W * fraction, bounds.H),
        FillOrigin.Top => new(bounds.X, bounds.Y, bounds.W, bounds.H * fraction),
        FillOrigin.Bottom => new(bounds.X, bounds.Y + bounds.H * (1f - fraction), bounds.W, bounds.H * fraction),
        _ => new(bounds.X, bounds.Y, bounds.W * fraction, bounds.H)
    };

    static void AddSegments(SKPathBuilder path, Rect bounds, float fraction, FillOptions options)
    {
        var vertical = options.Origin is FillOrigin.Top or FillOrigin.Bottom;
        var reversed = options.Origin is FillOrigin.Right or FillOrigin.Bottom;
        var count = Math.Clamp(options.Segments, 1, 4096);
        var length = vertical ? bounds.H : bounds.W;
        var gap = Math.Clamp(options.Gap, 0f, length / count);
        var cellLength = (length - gap * (count - 1)) / count;
        for (var i = 0; i < count; i++)
        {
            var cellValue = Math.Clamp(fraction * count - i, 0f, 1f);
            if (cellValue <= 0f)
            {
                break;
            }
            var offset = (reversed ? count - 1 - i : i) * (cellLength + gap);
            var cell = Cell(bounds, offset, cellLength, vertical);
            path.AddRect(Linear(cell, cellValue, options.Origin));
        }
    }

    static Rect Cell(Rect bounds, float offset, float length, bool vertical) => vertical
        ? new Rect(bounds.X, bounds.Y + offset, bounds.W, length)
        : new Rect(bounds.X + offset, bounds.Y, length, bounds.H);

    static void AddRadial(SKPathBuilder path, Rect bounds, float fraction, FillOptions options)
    {
        var sweep = Math.Clamp(options.SweepAngle, 0f, 360f) * fraction * (options.Clockwise ? 1f : -1f);
        if (sweep == 0f)
        {
            return;
        }
        var inner = Math.Clamp(options.InnerRadius, 0f, 1f);
        if (MathF.Abs(sweep) >= 360f)
        {
            path.AddOval(bounds);
            if (inner > 0f)
            {
                path.AddOval(Inner(bounds, inner));
                path.FillType = SKPathFillType.EvenOdd;
            }
            return;
        }
        path.ArcTo(bounds, options.StartAngle, sweep, true);
        if (inner > 0f)
        {
            path.LineTo(Point(bounds, inner, options.StartAngle + sweep));
            path.ArcTo(Inner(bounds, inner), options.StartAngle + sweep, -sweep, false);
        }
        else
        {
            path.LineTo(bounds.Center.X, bounds.Center.Y);
        }
        path.Close();
    }

    static Rect Inner(Rect bounds, float radius) => new(bounds.Center.X - bounds.W * radius / 2f,
        bounds.Center.Y - bounds.H * radius / 2f, bounds.W * radius, bounds.H * radius);

    static SKPoint Point(Rect bounds, float radius, float degrees)
    {
        var radians = degrees * MathF.PI / 180f;
        return new SKPoint(bounds.Center.X + MathF.Cos(radians) * bounds.W * radius / 2f,
            bounds.Center.Y + MathF.Sin(radians) * bounds.H * radius / 2f);
    }
}

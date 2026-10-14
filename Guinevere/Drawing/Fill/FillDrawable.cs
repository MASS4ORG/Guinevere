namespace Guinevere;

sealed class FillDrawable(Rect bounds, float fraction, Color color, FillOptions options) : IDrawable, IInkBounds
{
    /// <inheritdoc />
    public SKPaint? Paint => null;

    /// <inheritdoc />
    SKRect? IInkBounds.InkBounds(LayoutNode node) => bounds;

    /// <inheritdoc />
    public void Render(Gui gui, LayoutNode node, SKCanvas canvas)
    {
        using var path = FillGeometry.Create(bounds, fraction, options);
        var saved = canvas.Save();
        try
        {
            canvas.ClipPath(path, antialias: true);
            if (options.Shape is { } shape)
            {
                canvas.ClipPath(shape, antialias: true);
            }
            DrawContent(canvas);
        }
        finally
        {
            canvas.RestoreToCount(saved);
        }
    }

    void DrawContent(SKCanvas canvas)
    {
        if (options.Mask is null)
        {
            DrawSpriteOrColor(canvas);
            return;
        }
        var saved = canvas.SaveLayer((SKRect)bounds, null);
        try
        {
            DrawSpriteOrColor(canvas);
            using var maskPaint = new SKPaint { BlendMode = SKBlendMode.DstIn };
            canvas.DrawImage(options.Mask, bounds, new SKSamplingOptions(SKFilterMode.Linear), maskPaint);
        }
        finally
        {
            canvas.RestoreToCount(saved);
        }
    }

    void DrawSpriteOrColor(SKCanvas canvas)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        if (options.Sprite is not { } sprite)
        {
            canvas.DrawRect(bounds, paint);
            return;
        }
        using var tint = SKColorFilter.CreateBlendMode(color, SKBlendMode.Modulate);
        paint.Color = SKColors.White;
        paint.ColorFilter = tint;
        var source = options.Source ?? new Rect(0, 0, sprite.Width, sprite.Height);
        if (options.SpriteBorder is { } border)
        {
            DrawNineSlice(canvas, sprite, source, border, paint);
            return;
        }
        canvas.DrawImage(sprite, source, bounds, new SKSamplingOptions(SKFilterMode.Linear), paint);
    }

    void DrawNineSlice(SKCanvas canvas, SKImage sprite, Rect source, Insets border, SKPaint paint)
    {
        // The outer fill clip supplies antialiasing; patch edges must not fade or create seams at small sizes.
        paint.IsAntialias = false;
        var grid = new NineSliceLayout(source.W, source.H, bounds, border);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var (patch, destination) = grid.Patch(row, column);
                if (patch.W <= 0 || patch.H <= 0 || destination.W <= 0 || destination.H <= 0)
                {
                    continue;
                }
                patch.X += source.X;
                patch.Y += source.Y;
                canvas.DrawImage(sprite, patch, destination, new SKSamplingOptions(SKFilterMode.Linear), paint);
            }
        }
    }
}

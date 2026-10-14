using SkiaSharp;

namespace Guinevere;

/// <summary>A cached bitmap with logical sizing, positioning and independent repeat axes.</summary>
sealed class StyleBitmapBackground(SKImage image, (bool X, bool Y) repeat,
    (string Mode, BackgroundLength X, BackgroundLength Y) size,
    (BackgroundLength X, BackgroundLength Y) position, Insets? slice = null)
{
    internal static Insets? Slice(string? raw)
    {
        if (raw is null || raw.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = Parts(raw);
        if (parts.Length is < 1 or > 4) throw Syntax("slice", raw);
        Span<float> edges = stackalloc float[4];
        for (var i = 0; i < parts.Length; i++)
            edges[i] = SliceValue(parts[i]);
        return SliceInsets(edges, parts.Length);
    }

    static float SliceValue(string raw)
    {
        if (!StyleValue.TryLength(raw, out var value, out var percent)
            || percent || !float.IsFinite(value) || value < 0) throw Syntax("slice", raw);
        return value;
    }

    static Insets SliceInsets(ReadOnlySpan<float> edges, int count)
    {
        var top = edges[0];
        var right = count > 1 ? edges[1] : top;
        var bottom = count > 2 ? edges[2] : top;
        var left = count > 3 ? edges[3] : right;
        return new Insets(left, top, right, bottom);
    }

    internal static (bool X, bool Y) Repeat(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        null or "repeat" => (true, true),
        "repeat-x" => (true, false),
        "repeat-y" => (false, true),
        "no-repeat" => (false, false),
        _ => throw Syntax("repeat", raw),
    };

    internal static (string Mode, BackgroundLength X, BackgroundLength Y) Size(string? raw)
    {
        var parts = Parts(raw ?? "auto");
        if (parts.Length == 1 && parts[0] is "contain" or "cover") return (parts[0], default, default);
        return ExplicitSize(parts, raw);
    }

    static (string Mode, BackgroundLength X, BackgroundLength Y) ExplicitSize(string[] parts, string? raw)
    {
        if (parts.Length is < 1 or > 2) throw Syntax("size", raw);
        return ("explicit", Length(parts[0], "size", true),
            parts.Length == 2 ? Length(parts[1], "size", true) : new BackgroundLength(0, false, true));
    }

    internal static (BackgroundLength X, BackgroundLength Y) Position(string? raw)
    {
        var parts = Parts(raw ?? "0% 0%");
        if (parts.Length is < 1 or > 2) throw Syntax("position", raw);
        var x = parts[0];
        var y = parts.Length == 2 ? parts[1] : "center";
        (x, y) = Orient(x, y);
        return (Coordinate(x, true), Coordinate(y, false));
    }

    static (string X, string Y) Orient(string x, string y) =>
        x is "top" or "bottom" || y is "left" or "right" ? (y, x) : (x, y);

    static BackgroundLength Coordinate(string raw, bool horizontal) => raw == "center"
        ? new(0.5f, true)
        : AxisCoordinate(raw, horizontal);

    static BackgroundLength AxisCoordinate(string raw, bool horizontal)
    {
        if (raw == (horizontal ? "left" : "top")) return new(0, true);
        if (raw == (horizontal ? "right" : "bottom")) return new(1, true);
        return Length(raw, "position", false);
    }

    static BackgroundLength Length(string raw, string property, bool auto)
    {
        if (auto && raw == "auto") return new(0, false, true);
        if (!StyleValue.TryLength(raw, out var value, out var percent) || !float.IsFinite(value)
            || property == "size" && value <= 0) throw Syntax(property, raw);
        return new BackgroundLength(value, percent);
    }

    static string[] Parts(string raw) => raw.Trim().ToLowerInvariant()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    static BackgroundSyntaxException Syntax(string property, string? raw) =>
        new($"background-{property}", $"Unsupported background-{property} value '{raw}'.");

    internal SKShader CreateShader(SKRect bounds)
    {
        if (slice is { } border) return SliceShader(bounds, border);
        var dimensions = Dimensions(bounds);
        var x = bounds.Left + position.X.Resolve(bounds.Width - dimensions.X);
        var y = bounds.Top + position.Y.Resolve(bounds.Height - dimensions.Y);
        var matrix = SKMatrix.CreateScaleTranslation(dimensions.X / image.Width, dimensions.Y / image.Height, x, y);
        return image.ToShader(repeat.X ? SKShaderTileMode.Repeat : SKShaderTileMode.Decal,
            repeat.Y ? SKShaderTileMode.Repeat : SKShaderTileMode.Decal, matrix);
    }

    SKShader SliceShader(SKRect bounds, Insets border)
    {
        using var recorder = new SKPictureRecorder();
        var local = new SKRect(0, 0, Math.Max(0, bounds.Width), Math.Max(0, bounds.Height));
        var canvas = recorder.BeginRecording(local);
        NineSliceDrawable.Draw(canvas, image, new Rect(0, 0, local.Width, local.Height), border);
        using var picture = recorder.EndRecording();
        return picture.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
            SKFilterMode.Nearest, SKMatrix.CreateTranslation(bounds.Left, bounds.Top), local);
    }

    (float X, float Y) Dimensions(SKRect bounds)
    {
        if (size.Mode is "contain" or "cover")
        {
            var sx = bounds.Width / image.Width;
            var sy = bounds.Height / image.Height;
            var scale = size.Mode == "cover" ? Math.Max(sx, sy) : Math.Min(sx, sy);
            return (image.Width * scale, image.Height * scale);
        }
        return ExplicitDimensions(bounds);
    }

    (float X, float Y) ExplicitDimensions(SKRect bounds)
    {
        if (size.X.Auto && size.Y.Auto) return (image.Width, image.Height);
        var x = size.X.Resolve(bounds.Width);
        var y = size.Y.Resolve(bounds.Height);
        if (size.X.Auto) return (y * image.Width / image.Height, y);
        if (size.Y.Auto) return (x, x * image.Height / image.Width);
        return (x, y);
    }
}

readonly record struct BackgroundLength(float Value, bool Percent, bool Auto = false)
{
    internal float Resolve(float reference) => Percent ? reference * Value : Value;
}

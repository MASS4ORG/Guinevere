using System.Numerics;

namespace Guinevere;

/// <summary>
/// Builds a node transform from <c>scale</c>, <c>rotate</c>, <c>translate</c> and <c>transform-origin</c>, in the same
/// screen-space form <see cref="Gui.SetTransform"/> takes: scale and rotate about the origin, then translate.
/// </summary>
static class StyleTransform
{
    /// <summary>The transform the declarations describe for <paramref name="rect"/>, or <c>null</c> when none is set.</summary>
    internal static Matrix3x2? From(ResolvedStyle style, Rect rect)
    {
        var scale = style.Get("scale");
        var rotate = style.Get("rotate");
        var translate = style.Get("translate");
        if (scale is null && rotate is null && translate is null) return null;

        var origin = Origin(style.Get("transform-origin"), rect);
        var (scaleX, scaleY) = Pair(scale, 1f, sameWhenSingle: true);
        var (moveX, moveY) = Pair(translate, 0f, sameWhenSingle: false);
        return Matrix3x2.CreateScale(scaleX, scaleY, origin)
               * Matrix3x2.CreateRotation(Radians(rotate), origin)
               * Matrix3x2.CreateTranslation(moveX, moveY);
    }

    /// <summary>One or two numbers; a single value repeats for scale and leaves y at the default for translate.</summary>
    static (float X, float Y) Pair(string? text, float fallback, bool sameWhenSingle)
    {
        if (text is null) return (fallback, fallback);
        var parts = Split(text);
        var x = parts.Length > 0 && StyleValue.TryLength(parts[0], out var first, out _) ? first : fallback;
        if (parts.Length < 2) return (x, sameWhenSingle ? x : fallback);
        return (x, StyleValue.TryLength(parts[1], out var second, out _) ? second : fallback);
    }

    /// <summary>An angle in <c>deg</c> (also the unit of a bare number), <c>rad</c> or <c>turn</c>.</summary>
    static float Radians(string? text)
    {
        if (text is null) return 0f;
        var t = text.Trim();
        var (number, scale) = t.EndsWith("turn", StringComparison.OrdinalIgnoreCase) ? (t[..^4], MathF.Tau)
            : t.EndsWith("rad", StringComparison.OrdinalIgnoreCase) ? (t[..^3], 1f)
            : t.EndsWith("deg", StringComparison.OrdinalIgnoreCase) ? (t[..^3], MathF.PI / 180f)
            : (t, MathF.PI / 180f);
        return StyleValue.TryFloat(number, out var value) ? value * scale : 0f;
    }

    /// <summary>
    /// The pivot inside <paramref name="rect"/>: keywords (<c>left</c>, <c>top</c>, <c>center</c>, …), percentages of
    /// the box or pixel offsets from its top-left corner, x first. The default is the center.
    /// </summary>
    static Vector2 Origin(string? text, Rect rect)
    {
        var parts = text is null ? [] : Split(text);
        var vertical = parts.Length == 1 && parts[0] is "top" or "bottom";
        var x = parts.Length == 0 || vertical ? "center" : parts[0];
        var y = vertical ? parts[0] : parts.Length >= 2 ? parts[1] : "center";
        return new Vector2(rect.X + Offset(x, rect.W), rect.Y + Offset(y, rect.H));
    }

    /// <summary>A keyword or percentage as a fraction of <paramref name="size"/>, or a pixel offset.</summary>
    static float Offset(string token, float size) => token switch
    {
        "left" or "top" => 0f,
        "right" or "bottom" => size,
        _ when StyleValue.TryLength(token, out var value, out var percent) => percent ? value * size : value,
        _ => size * 0.5f,
    };

    static string[] Split(string text) =>
        text.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

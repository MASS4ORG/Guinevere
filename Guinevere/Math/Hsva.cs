namespace Guinevere;

/// <summary>
/// A color as hue, saturation, value and straight alpha. Hue is in degrees [0, 360); the rest are in [0, 1]. Editors
/// keep this form between frames because a gray or black <see cref="Color"/> has no hue or saturation to return to.
/// </summary>
/// <param name="H">Hue in degrees.</param>
/// <param name="S">Saturation.</param>
/// <param name="V">Value (brightness).</param>
/// <param name="A">Alpha.</param>
public readonly record struct Hsva(float H, float S, float V, float A = 1f)
{
    /// <summary>Converts a color, using <paramref name="hueHint"/> and <paramref name="saturationHint"/> where the color has none.</summary>
    /// <param name="color">The color to convert.</param>
    /// <param name="hueHint">Hue kept for grays, whose hue is undefined.</param>
    /// <param name="saturationHint">Saturation kept for black, whose saturation is undefined.</param>
    public static Hsva FromColor(Color color, float hueHint = 0f, float saturationHint = 0f) =>
        FromRgb(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f, hueHint, saturationHint);

    /// <summary>Converts red, green and blue in [0, 1], with the same hints as <see cref="FromColor"/>.</summary>
    /// <param name="r">Red.</param>
    /// <param name="g">Green.</param>
    /// <param name="b">Blue.</param>
    /// <param name="alpha">Alpha.</param>
    /// <param name="hueHint">Hue kept for grays.</param>
    /// <param name="saturationHint">Saturation kept for black.</param>
    public static Hsva FromRgb(float r, float g, float b, float alpha = 1f, float hueHint = 0f, float saturationHint = 0f)
    {
        r = Math.Clamp(r, 0f, 1f);
        g = Math.Clamp(g, 0f, 1f);
        b = Math.Clamp(b, 0f, 1f);
        var max = MathF.Max(r, MathF.Max(g, b));
        var chroma = max - MathF.Min(r, MathF.Min(g, b));
        if (max <= 0f)
        {
            return new Hsva(hueHint, saturationHint, 0f, alpha);
        }

        if (chroma <= 0f)
        {
            return new Hsva(hueHint, 0f, max, alpha);
        }

        var hue = max == r ? (g - b) / chroma
            : max == g ? 2f + (b - r) / chroma
            : 4f + (r - g) / chroma;
        return new Hsva(WrapHue(hue * 60f), chroma / max, max, alpha);
    }

    /// <summary>
    /// Returns <paramref name="current"/> while it still produces <paramref name="color"/>, otherwise the color converted
    /// with the current hue and saturation as hints. Call it every frame to follow an externally owned color.
    /// </summary>
    /// <param name="current">The editor's value from the previous frame.</param>
    /// <param name="color">The color the caller owns.</param>
    public static Hsva Sync(Hsva current, Color color) =>
        current.ToColor() == color ? current : FromColor(color, current.H, current.S);

    /// <summary>Converts to an sRGB color, clamping every component into range.</summary>
    public Color ToColor()
    {
        var (r, g, b) = Rgb();
        return Color.FromArgb(ToByte(A), ToByte(r), ToByte(g), ToByte(b));
    }

    /// <summary>The red, green and blue components in [0, 1].</summary>
    public (float R, float G, float B) Rgb()
    {
        var s = Math.Clamp(S, 0f, 1f);
        var v = Math.Clamp(V, 0f, 1f);
        var sector = WrapHue(H) / 60f;
        var chroma = v * s;
        var x = chroma * (1f - MathF.Abs(sector % 2f - 1f));
        var m = v - chroma;
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x)
        };
        return (r + m, g + m, b + m);
    }

    /// <summary>A copy with each component wrapped or clamped into its range.</summary>
    public Hsva Normalized() =>
        new(WrapHue(H), Math.Clamp(S, 0f, 1f), Math.Clamp(V, 0f, 1f), Math.Clamp(A, 0f, 1f));

    static float WrapHue(float hue)
    {
        if (!float.IsFinite(hue))
        {
            return 0f;
        }

        var wrapped = hue % 360f;
        return wrapped < 0f ? wrapped + 360f : wrapped;
    }

    static int ToByte(float value) => (int)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
}

using System.Globalization;

namespace Guinevere;

/// <summary>A straight-alpha color with 0..1 channels, used while evaluating <c>.pss</c> color functions.</summary>
readonly record struct StyleColor(double R, double G, double B, double A)
{
    public static StyleColor From(Color color) => new(color.R / 255d, color.G / 255d, color.B / 255d, color.A / 255d);

    /// <summary>Canonical <c>#rrggbbaa</c> text, which <see cref="StyleValue.TryColor"/> reads back exactly.</summary>
    public string ToHex() => string.Create(CultureInfo.InvariantCulture,
        $"#{Channel(R):x2}{Channel(G):x2}{Channel(B):x2}{Channel(A):x2}");

    public StyleColor Lerp(StyleColor to, double t) =>
        new(R + (to.R - R) * t, G + (to.G - G) * t, B + (to.B - B) * t, A + (to.A - A) * t);

    public StyleColor WithAlpha(double alpha) => this with { A = Math.Clamp(alpha, 0d, 1d) };

    /// <summary>WCAG relative luminance of the color, ignoring alpha.</summary>
    public double Luminance() => 0.2126 * Linear(R) + 0.7152 * Linear(G) + 0.0722 * Linear(B);

    public (double H, double S, double V) ToHsv()
    {
        var max = Math.Max(R, Math.Max(G, B));
        var delta = max - Math.Min(R, Math.Min(G, B));
        var saturation = max <= 0d ? 0d : delta / max;
        return (Hue(max, delta), saturation, max);
    }

    public static StyleColor FromHsv(double h, double s, double v, double a)
    {
        var chroma = v * s;
        return FromChroma(h, chroma, v - chroma, a);
    }

    public static StyleColor FromHsl(double h, double s, double l, double a)
    {
        var chroma = (1d - Math.Abs(2d * l - 1d)) * s;
        return FromChroma(h, chroma, l - chroma / 2d, a);
    }

    double Hue(double max, double delta)
    {
        if (delta <= 0d) return 0d;
        var sector = max == R ? (G - B) / delta % 6d : max == G ? (B - R) / delta + 2d : (R - G) / delta + 4d;
        var hue = sector * 60d;
        return hue < 0d ? hue + 360d : hue;
    }

    static StyleColor FromChroma(double h, double chroma, double m, double a)
    {
        var sector = (h % 360d + 360d) % 360d / 60d;
        var x = chroma * (1d - Math.Abs(sector % 2d - 1d));
        var (r, g, b) = (int)sector switch
        {
            0 => (chroma, x, 0d),
            1 => (x, chroma, 0d),
            2 => (0d, chroma, x),
            3 => (0d, x, chroma),
            4 => (x, 0d, chroma),
            _ => (chroma, 0d, x),
        };
        return new StyleColor(r + m, g + m, b + m, a);
    }

    static double Linear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    static int Channel(double value) => (int)Math.Round(Math.Clamp(value, 0d, 1d) * 255d);
}

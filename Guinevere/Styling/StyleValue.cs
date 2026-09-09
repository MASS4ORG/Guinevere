using System.Globalization;

namespace Guinevere;

/// <summary>Parsers for the scalar value forms used in <c>.uss</c> declarations.</summary>
public static class StyleValue
{
    /// <summary>
    /// Parses a length: a bare number or <c>Npx</c> → pixels; <c>N%</c> → the 0..1 fraction via
    /// <paramref name="isPercent"/>.
    /// </summary>
    /// <param name="text">The length text.</param>
    /// <param name="value">Pixels, or the 0..1 fraction for a percentage.</param>
    /// <param name="isPercent">True when the text ended with <c>%</c>.</param>
    public static bool TryLength(string? text, out float value, out bool isPercent)
    {
        value = 0f;
        isPercent = false;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var t = text.Trim();
        if (t.EndsWith('%'))
        {
            isPercent = true;
            if (!float.TryParse(t[..^1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;
            value /= 100f;
            return true;
        }

        if (t.EndsWith("px", StringComparison.OrdinalIgnoreCase)) t = t[..^2].Trim();
        return float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Parses a bare float (invariant culture).</summary>
    /// <param name="text">The number text.</param>
    /// <param name="value">The parsed value.</param>
    public static bool TryFloat(string? text, out float value) =>
        float.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>Parses a bool: <c>true</c>/<c>false</c>/<c>1</c>/<c>0</c>/<c>yes</c>/<c>no</c>/<c>on</c>/<c>off</c>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The parsed bool.</param>
    public static bool TryBool(string? text, out bool value)
    {
        var t = (text ?? string.Empty).Trim();
        if (Eq(t, "true") || t == "1" || Eq(t, "yes") || Eq(t, "on")) { value = true; return true; }
        if (Eq(t, "false") || t == "0" || Eq(t, "no") || Eq(t, "off")) { value = false; return true; }
        value = false;
        return false;

        static bool Eq(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a colour: <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb(r,g,b)</c>,
    /// <c>rgba(r,g,b,a)</c> (a in 0..1 or 0..255), or a named colour.
    /// </summary>
    /// <param name="text">The colour text.</param>
    /// <param name="color">The parsed colour.</param>
    public static bool TryColor(string? text, out Color color)
    {
        color = Color.Black;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();

        if (t.StartsWith('#'))
        {
            var hex = t[1..];
            if (hex.Length == 3)
                hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);

            if ((hex.Length == 6 || hex.Length == 8)
                && int.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
                && int.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
                && int.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                var a = 255;
                if (hex.Length == 8)
                    _ = int.TryParse(hex.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a);
                color = Color.FromArgb(a, r, g, b);
                return true;
            }

            return false;
        }

        if (t.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = t.IndexOf('(');
            var close = t.IndexOf(')');
            if (open < 0 || close < open) return false;

            var parts = t[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length is < 3 or > 4) return false;
            if (!TryFloat(parts[0], out var rr) || !TryFloat(parts[1], out var gg) || !TryFloat(parts[2], out var bb))
                return false;

            var aa = 255f;
            if (parts.Length == 4 && TryFloat(parts[3], out var av))
                aa = av <= 1f ? av * 255f : av;

            color = Color.FromArgb((int)aa, (int)rr, (int)gg, (int)bb);
            return true;
        }

        if (t.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.Transparent;
            return true;
        }

        var named = System.Drawing.Color.FromName(t);
        if (named.A == 0 && !t.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            return false;

        color = named;
        return true;
    }
}

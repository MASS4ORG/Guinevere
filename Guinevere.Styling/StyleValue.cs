using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Guinevere;

/// <summary>Parsers for the scalar value forms used in <c>.pss</c> declarations.</summary>
public static class StyleValue
{
    /// <summary>
    /// Formats a typed value as <c>.pss</c> text: colors as <c>#rrggbbaa</c>, numbers in invariant culture, booleans
    /// as <c>true</c>/<c>false</c> and vectors as <c>(x, y)</c> tuples.
    /// </summary>
    /// <param name="value">A call-site or host value.</param>
    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool flag => flag ? "true" : "false",
        _ => FormatTyped(value),
    };

    static string FormatTyped(object value) => value switch
    {
        Color color => StyleColor.From(color).ToHex(),
        System.Drawing.Color color => StyleColor.From(color).ToHex(),
        System.Numerics.Vector2 vector => string.Create(CultureInfo.InvariantCulture, $"({vector.X}, {vector.Y})"),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// Parses a length: a bare number or <c>Npx</c> → pixels; <c>N%</c> → the 0..1 fraction via
    /// <paramref name="isPercent"/>.
    /// </summary>
    /// <param name="text">The length text.</param>
    /// <param name="value">Pixels, or the 0..1 fraction for a percentage.</param>
    /// <param name="isPercent">True when the text ended with <c>%</c>.</param>
    public static bool TryLength(string? text, out float value, out bool isPercent)
        => TryLength(text.AsSpan(), out value, out isPercent);

    internal static bool TryLength(ReadOnlySpan<char> text, out float value, out bool isPercent)
    {
        value = 0f;
        isPercent = false;
        if (text.IsWhiteSpace()) return false;

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
        TryFloat(text.AsSpan(), out value);

    internal static bool TryFloat(ReadOnlySpan<char> text, out float value) =>
        float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

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
    /// Parses <c>url("…")</c>, <c>url(…)</c> or a quoted string, resolving relative locations against
    /// <paramref name="baseUri"/> when it is absolute.
    /// </summary>
    /// <param name="text">The URL text.</param>
    /// <param name="baseUri">The owning sheet's base, or <c>null</c> to keep relative locations relative.</param>
    /// <param name="uri">The parsed location.</param>
    public static bool TryUrl(string? text, Uri? baseUri, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        if (t.StartsWith("url(", StringComparison.OrdinalIgnoreCase) && t.EndsWith(')')) t = t[4..^1].Trim();
        t = Unquote(t);
        if (t.Length == 0) return false;
        if (Uri.TryCreate(t, UriKind.Absolute, out uri)) return true;
        return baseUri is { IsAbsoluteUri: true }
            ? Uri.TryCreate(baseUri, t, out uri)
            : Uri.TryCreate(t, UriKind.Relative, out uri);
    }

    /// <summary>Removes one pair of matching surrounding <c>"</c> or <c>'</c> quotes.</summary>
    /// <param name="text">The possibly quoted text.</param>
    public static string Unquote(string text)
    {
        var t = text.Trim();
        return t.Length >= 2 && t[0] is '"' or '\'' && t[^1] == t[0] ? t[1..^1] : t;
    }

    /// <summary>
    /// Resolves CSS escapes: <c>\</c> followed by 1–6 hex digits (and one optional space) is that code point, such as
    /// <c>"\f07b"</c> for an icon glyph; <c>\</c> followed by any other character is that character, such as
    /// <c>scene\.move</c>.
    /// </summary>
    /// <param name="text">The escaped text.</param>
    public static string Unescape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.Contains('\\')) return text;
        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length) i = AppendEscape(builder, text, i + 1);
            else builder.Append(text[i]);
        }
        return builder.ToString();
    }

    /// <summary>Appends the escape that starts after a backslash; returns the index of its last character.</summary>
    static int AppendEscape(System.Text.StringBuilder builder, string text, int start)
    {
        var end = start;
        while (end - start < 6 && end < text.Length && char.IsAsciiHexDigit(text[end])) end++;
        if (end == start)
        {
            builder.Append(text[start]);
            return start;
        }

        var codePoint = int.Parse(text.AsSpan(start, end - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        builder.Append(IsScalar(codePoint) ? char.ConvertFromUtf32(codePoint) : "�");
        return end < text.Length && text[end] == ' ' ? end : end - 1;
    }

    static bool IsScalar(int codePoint) => codePoint is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF);

    /// <summary>
    /// Parses a color: <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb(r,g,b)</c>/<c>rgba(r,g,b,a)</c> with
    /// every channel in 0..255, <c>rgb1()</c>/<c>rgba1()</c> with every channel in 0..1, or a named color.
    /// </summary>
    /// <param name="text">The color text.</param>
    /// <param name="color">The parsed color.</param>
    public static bool TryColor(string? text, out Color color)
    {
        color = Color.Black;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();

        if (t.StartsWith('#'))
        {
            return Color.TryParseHex(t, out color);
        }

        if (t.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            return TryRgb(t, out color);

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

    /// <summary>Literal <c>rgb</c>/<c>rgba</c> (0..255 channels) and <c>rgb1</c>/<c>rgba1</c> (0..1 channels).</summary>
    static bool TryRgb(string t, out Color color)
    {
        color = Color.Black;
        var open = t.IndexOf('(');
        var close = t.IndexOf(')');
        if (open < 0 || close < open) return false;

        var scale = RgbScale(t[..open]);
        Span<float> channels = [0f, 0f, 0f, 255f / Math.Max(scale, 1f)];
        if (scale == 0f || !TryChannels(t[(open + 1)..close], channels)) return false;

        color = Color.FromArgb(Byte(channels[3]), Byte(channels[0]), Byte(channels[1]), Byte(channels[2]));
        return true;

        int Byte(float channel) => (int)Math.Clamp(MathF.Round(channel * scale), 0f, 255f);
    }

    /// <summary>Parses three or four comma-separated numbers over the given defaults.</summary>
    static bool TryChannels(string list, Span<float> channels)
    {
        var parts = list.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is < 3 or > 4) return false;
        for (var i = 0; i < parts.Length; i++)
            if (!TryFloat(parts[i], out channels[i])) return false;
        return true;
    }

    /// <summary>The factor from a function's channel range to 0..255, or 0 for an unknown name.</summary>
    static float RgbScale(string name) => name.Trim().ToLowerInvariant() switch
    {
        "rgb" or "rgba" => 1f,
        "rgb1" or "rgba1" => 255f,
        _ => 0f,
    };
}

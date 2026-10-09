using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>
/// Splits a declaration value into numeric channels and the text around them, so two values that differ only in
/// their numbers and colors can be interpolated: <c>0 2 8 #00000080</c> and <c>0 6 16 #00000040</c> share a shape.
/// </summary>
static partial class StyleValueChannels
{
    const char NumberSlot = '\u0001';
    const char ColorSlot = '\u0002';

    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b|[-+]?(?:\d+\.?\d*|\.\d+)")]
    private static partial Regex Token();

    /// <summary>Splits <paramref name="value"/>; colors take four channels (red, green, blue, alpha, 0..255).</summary>
    /// <returns>Whether the value has anything to interpolate.</returns>
    internal static bool TryParse(string value, out string shape, out float[] channels)
    {
        if (!value.Contains('#') && StyleValue.TryColor(value, out var named))
        {
            (shape, channels) = (ColorSlot.ToString(), [named.R, named.G, named.B, named.A]);
            return true;
        }

        var builder = new StringBuilder(value.Length);
        var list = new List<float>();
        var start = 0;
        foreach (Match match in Token().Matches(value))
        {
            builder.Append(value, start, match.Index - start);
            start = match.Index + match.Length;
            if (match.Value[0] != '#')
            {
                list.Add(float.Parse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture));
                builder.Append(NumberSlot);
            }
            else if (Color.TryParseHex(match.Value, out var color))
            {
                list.AddRange([color.R, color.G, color.B, color.A]);
                builder.Append(ColorSlot);
            }
            else builder.Append(match.Value);
        }
        builder.Append(value, start, value.Length - start);
        (shape, channels) = (builder.ToString(), [.. list]);
        return channels.Length > 0;
    }

    /// <summary>Writes <paramref name="channels"/> back into <paramref name="shape"/>.</summary>
    internal static string Format(string shape, ReadOnlySpan<float> channels)
    {
        var builder = new StringBuilder(shape.Length + channels.Length * 4);
        var next = 0;
        foreach (var c in shape)
        {
            if (c == NumberSlot) builder.Append(channels[next++].ToString("0.####", CultureInfo.InvariantCulture));
            else if (c == ColorSlot)
            {
                builder.Append('#');
                for (var i = 0; i < 4; i++)
                    builder.Append(((int)Math.Clamp(MathF.Round(channels[next++]), 0f, 255f)).ToString("x2",
                        CultureInfo.InvariantCulture));
            }
            else builder.Append(c);
        }
        return builder.ToString();
    }
}

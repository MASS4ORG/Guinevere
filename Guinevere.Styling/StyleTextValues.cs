using System.Numerics;
using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>Parses text shadow layers into the core renderer's Gaussian shadows.</summary>
static class StyleTextValues
{
    /// <summary>Reads CSS <c>x y [blur] [color]</c> layers, first on top; malformed declarations are ignored.</summary>
    public static IReadOnlyList<TextEffects.TextShadow>? Shadows(string? value, Color color)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var layers = StyleBoxValues.Split(value, ',');
        if (layers.Count == 0 || Regex.IsMatch(value, @"(^|,)\s*(,|$)"))
        {
            return null;
        }

        var shadows = new List<TextEffects.TextShadow>(layers.Count);
        foreach (var layer in layers)
        {
            if (Shadow(layer, color) is not { } shadow)
            {
                return null;
            }

            shadows.Add(shadow);
        }
        return shadows.AsReadOnly();
    }

    static TextEffects.TextShadow? Shadow(string layer, Color fallback)
    {
        var lengths = new List<float>(3);
        Color? color = null;
        foreach (var token in StyleBoxValues.Split(layer, ' '))
        {
            if (TryPixels(token, out var length))
            {
                lengths.Add(length);
            }
            else if (color is null && StyleValue.TryColor(token, out var parsed))
            {
                color = parsed;
            }
            else
            {
                return null;
            }
        }
        return CreateShadow(lengths, color ?? fallback);
    }

    static bool TryPixels(string token, out float length) =>
        StyleValue.TryLength(token, out length, out var percent) && !percent && float.IsFinite(length);

    static TextEffects.TextShadow? CreateShadow(List<float> lengths, Color color)
    {
        if (lengths.Count is < 2 or > 3 || lengths.ElementAtOrDefault(2) < 0f)
        {
            return null;
        }

        return new TextEffects.TextShadow(color, new Vector2(lengths[0], lengths[1]),
            lengths.ElementAtOrDefault(2) * 0.5f);
    }
}

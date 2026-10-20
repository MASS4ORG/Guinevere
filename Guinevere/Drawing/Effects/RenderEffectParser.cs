using System.Globalization;
using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>Parses ordered filter functions with numeric arguments and hexadecimal colors.</summary>
public static partial class RenderEffectParser
{
    /// <summary>Parses a filter list; accepts px, deg and percentage suffixes, or none for an empty stack.</summary>
    public static RenderEffectStack Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (Empty(source))
        {
            return new RenderEffectStack();
        }
        var effects = new List<RenderEffect>();
        var end = 0;
        foreach (Match match in Function().Matches(source))
        {
            ValidateGap(source[end..match.Index]);
            effects.Add(ParseFunction(match.Groups[1].Value,
                match.Groups[2].Value.Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries)));
            end = match.Index + match.Length;
        }
        ValidateGap(source[end..]);
        if (effects.Count == 0)
        {
            throw new FormatException("Invalid render filter list.");
        }
        return new RenderEffectStack([.. effects]);
    }

    static bool Empty(string source) => source.Trim() is "none" or "";

    static void ValidateGap(string gap)
    {
        if (!string.IsNullOrWhiteSpace(gap))
        {
            throw new FormatException("Expected a render filter function.");
        }
    }

    static RenderEffect ParseFunction(string name, string[] args)
    {
        var count = name switch { "glow" or "tint" => 2, "drop-shadow" => 4, "chromatic-aberration" => 2, _ => 1 };
        if (args.Length != count)
        {
            throw new FormatException($"{name} requires {count} arguments.");
        }
        return name switch
        {
            "blur" => new RenderEffect.Blur(Number(args[0])),
            "glow" => new RenderEffect.Glow(Number(args[0]), Hex(args[1])),
            "drop-shadow" => new RenderEffect.DropShadow(Number(args[0]), Number(args[1]), Number(args[2]), Hex(args[3])),
            "saturate" => new RenderEffect.Saturation(Number(args[0])),
            "brightness" => new RenderEffect.Brightness(Number(args[0])),
            "contrast" => new RenderEffect.Contrast(Number(args[0])),
            "tint" => new RenderEffect.Tint(Hex(args[0]), Number(args[1])),
            "hue-rotate" => new RenderEffect.HueRotation(Number(args[0])),
            "grayscale" => new RenderEffect.Grayscale(Number(args[0])),
            "invert" => new RenderEffect.Invert(Number(args[0])),
            "pixelate" => new RenderEffect.Pixelate(Number(args[0])),
            "chromatic-aberration" => new RenderEffect.ChromaticAberration(Number(args[0]), Number(args[1])),
            "sharpen" => new RenderEffect.Sharpen(Number(args[0])),
            _ => throw new FormatException($"Unknown render filter: {name}."),
        };
    }

    static float Number(string value)
    {
        var percent = value.EndsWith('%');
        var number = value.EndsWith("px", StringComparison.Ordinal) ? value[..^2]
            : value.EndsWith("deg", StringComparison.Ordinal) ? value[..^3]
            : percent ? value[..^1] : value;
        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            || !float.IsFinite(result))
        {
            throw new FormatException("Filter arguments must be finite numbers.");
        }
        return percent ? result / 100 : result;
    }

    static Color Hex(string value) => Color.TryParseHex(value, out var color) ? color
        : throw new FormatException("Filter colors must be hexadecimal.");

    [GeneratedRegex(@"([a-z-]+)\(([^()]*)\)")]
    private static partial Regex Function();
}

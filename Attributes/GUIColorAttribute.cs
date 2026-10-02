namespace MASS4.Attributes;

/// <summary>Tints the field's controls (text and surface); nested content inherits the tint.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class GuiColorAttribute : Attribute
{
    /// <summary>A tint from channels in 0–1.</summary>
    public GuiColorAttribute(float r, float g, float b, float a = 1f) => (R, G, B, A) = (r, g, b, a);

    /// <summary>
    /// A tint from <c>#RRGGBB</c> or <c>#RRGGBBAA</c>. Malformed text gives magenta, so the mistake shows on
    /// screen instead of breaking the form at reflection time.
    /// </summary>
    public GuiColorAttribute(string hex) => (R, G, B, A) = Parse(hex) ?? (1f, 0f, 1f, 1f);

    /// <summary>Red, 0–1.</summary>
    public float R { get; }

    /// <summary>Green, 0–1.</summary>
    public float G { get; }

    /// <summary>Blue, 0–1.</summary>
    public float B { get; }

    /// <summary>Alpha, 0–1.</summary>
    public float A { get; }

    static (float, float, float, float)? Parse(string? hex)
    {
        var digits = hex?.TrimStart('#') ?? "";
        if (digits.Length is not (6 or 8) || !uint.TryParse(digits, System.Globalization.NumberStyles.HexNumber,
                null, out var value))
            return null;

        if (digits.Length == 6) value = (value << 8) | 0xFF;
        return ((value >> 24) / 255f, ((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f,
            (value & 0xFF) / 255f);
    }
}

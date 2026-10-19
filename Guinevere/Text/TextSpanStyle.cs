namespace Guinevere;

/// <summary>Lines drawn across a text span.</summary>
[Flags]
public enum TextDecoration
{
    /// <summary>No decoration.</summary>
    None = 0,
    /// <summary>A line under the baseline.</summary>
    Underline = 1,
    /// <summary>A line through the middle of the glyphs.</summary>
    Strikethrough = 2,
    /// <summary>A line above the glyphs.</summary>
    Overline = 4
}

/// <summary>Horizontal placement of each line inside a text block.</summary>
public enum TextAlignment
{
    /// <summary>Lines start at the left edge.</summary>
    Start,
    /// <summary>Lines are centered.</summary>
    Center,
    /// <summary>Lines end at the right edge.</summary>
    End
}

/// <summary>
/// Visual style of a range of rich text. Unset properties inherit from the spans below and finally from the scope's
/// text font, size and color, so overlapping spans compose.
/// </summary>
public readonly record struct TextSpanStyle
{
    /// <summary>Font family face, or null to inherit.</summary>
    public Font? Font { get; init; }
    /// <summary>Logical font size, or null to inherit.</summary>
    public float? Size { get; init; }
    /// <summary>Glyph color, or null to inherit.</summary>
    public Color? Color { get; init; }
    /// <summary>Numeric weight such as 700 for bold, or null to inherit.</summary>
    public int? Weight { get; init; }
    /// <summary>Whether glyphs are slanted, or null to inherit.</summary>
    public bool? Italic { get; init; }
    /// <summary>Decoration lines, or null to inherit.</summary>
    public TextDecoration? Decoration { get; init; }
    /// <summary>Decoration line color, or null to use the glyph color.</summary>
    public Color? DecorationColor { get; init; }
    /// <summary>Fill drawn behind the span, or null for none.</summary>
    public Color? Background { get; init; }
    /// <summary>Logical pixels the span is raised above the baseline; negative values lower it.</summary>
    public float? BaselineShift { get; init; }

    /// <summary>Bold text.</summary>
    public static TextSpanStyle Bold => new() { Weight = 700 };
    /// <summary>Italic text.</summary>
    public static TextSpanStyle Slanted => new() { Italic = true };
    /// <summary>Underlined text.</summary>
    public static TextSpanStyle Underlined => new() { Decoration = TextDecoration.Underline };
    /// <summary>Struck-through text.</summary>
    public static TextSpanStyle Struck => new() { Decoration = TextDecoration.Strikethrough };

    /// <summary>Text drawn in <paramref name="color"/>.</summary>
    public static TextSpanStyle Colored(Color color) => new() { Color = color };

    /// <summary>This style with every property <paramref name="over"/> sets replacing its own.</summary>
    public TextSpanStyle With(TextSpanStyle over) => new()
    {
        Font = Pick(over.Font, Font),
        Size = Pick(over.Size, Size),
        Color = Pick(over.Color, Color),
        Weight = Pick(over.Weight, Weight),
        Italic = Pick(over.Italic, Italic),
        Decoration = Pick(over.Decoration, Decoration),
        DecorationColor = Pick(over.DecorationColor, DecorationColor),
        Background = Pick(over.Background, Background),
        BaselineShift = Pick(over.BaselineShift, BaselineShift),
    };

    static T? Pick<T>(T? over, T? under) where T : struct => over ?? under;

    static T? Pick<T>(T? over, T? under) where T : class => over ?? under;
}

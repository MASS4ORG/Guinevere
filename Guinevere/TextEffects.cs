namespace Guinevere;

/// <summary>
/// Optional visual effects for <see cref="Gui.DrawText(string, float, Color?, Font?, float, bool, bool, TextEffects?)"/>:
/// an outline, a drop shadow, an inner shadow and a gradient fill. Any combination may be set;
/// they are drawn shadow → outline → fill → inner-shadow.
/// </summary>
public readonly record struct TextEffects
{
    /// <summary>Stroked outline drawn behind the fill.</summary>
    public TextOutline? Outline { get; init; }

    /// <summary>Blurred, offset copy drawn behind everything.</summary>
    public TextShadow? DropShadow { get; init; }

    /// <summary>Blurred, offset copy clipped to the glyphs and drawn on top of the fill.</summary>
    public TextShadow? InnerShadow { get; init; }

    /// <summary>Gradient used for the fill instead of the flat text color.</summary>
    public TextGradient? Gradient { get; init; }

    /// <summary>An outline: a fill of <paramref name="Color"/> stroked <paramref name="Width"/> pixels wide.</summary>
    /// <param name="Color">Outline color.</param>
    /// <param name="Width">Stroke width in pixels.</param>
    public readonly record struct TextOutline(Color Color, float Width);

    /// <summary>A shadow: a copy of the text in <paramref name="Color"/>, offset and blurred.</summary>
    /// <param name="Color">Shadow color (usually semi-transparent).</param>
    /// <param name="Offset">Pixel offset from the text.</param>
    /// <param name="Blur">Gaussian blur sigma; 0 for a hard shadow.</param>
    public readonly record struct TextShadow(Color Color, Vector2 Offset, float Blur);

    /// <summary>A two-stop gradient fill over the text's rectangle.</summary>
    /// <param name="From">Color at the start.</param>
    /// <param name="To">Color at the end.</param>
    /// <param name="AngleDegrees">Direction, 0 = left→right, 90 = top→bottom. Ignored when <paramref name="Radial"/>.</param>
    /// <param name="Radial">When <c>true</c>, a center-out radial gradient instead of linear.</param>
    public readonly record struct TextGradient(Color From, Color To, float AngleDegrees = 0f, bool Radial = false);
}

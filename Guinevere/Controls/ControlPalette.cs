namespace Guinevere;

/// <summary>
/// Fallback colours the built-in controls use when a call does not name its own. Set
/// <see cref="Gui.Controls"/> once at startup and every checkbox, toggle, dropdown and text field
/// follows, instead of each caller repeating a palette.
/// </summary>
public sealed class ControlPalette
{
    /// <summary>Fill of an input, a dropdown button or an unchecked box.</summary>
    public Color Surface { get; init; } = Color.White;

    /// <summary>Fill of a surface under the pointer.</summary>
    public Color SurfaceHover { get; init; } = Color.FromArgb(255, 248, 248, 248);

    /// <summary>Fill of a list or popup opened by a control.</summary>
    public Color Popup { get; init; } = Color.White;

    /// <summary>Outline of a control at rest.</summary>
    public Color Border { get; init; } = Color.Gray;

    /// <summary>Outline and ring of a focused control.</summary>
    public Color Accent { get; init; } = Color.FromArgb(255, 100, 149, 237);

    /// <summary>Main text.</summary>
    public Color Text { get; init; } = Color.Black;

    /// <summary>Placeholder and secondary text.</summary>
    public Color TextDim { get; init; } = Color.Gray;

    /// <summary>Fill of a checked box, an on toggle or a selected row.</summary>
    public Color Selected { get; init; } = Color.FromArgb(255, 100, 149, 237);

    /// <summary>The knob of a toggle and the tick of a checkbox.</summary>
    public Color Knob { get; init; } = Color.White;

    /// <summary>The default light palette, matching the built-in controls' historical colours.</summary>
    public static ControlPalette Light { get; } = new();

    /// <summary>A dark palette for editor-style hosts.</summary>
    public static ControlPalette Dark { get; } = new()
    {
        Surface = Color.FromArgb(255, 28, 31, 37),
        SurfaceHover = Color.FromArgb(255, 38, 42, 50),
        Popup = Color.FromArgb(255, 32, 36, 43),
        Border = Color.FromArgb(255, 51, 56, 66),
        Accent = Color.FromArgb(255, 84, 143, 224),
        Text = Color.FromArgb(255, 215, 218, 224),
        TextDim = Color.FromArgb(255, 139, 146, 156),
        Selected = Color.FromArgb(255, 84, 143, 224),
        Knob = Color.FromArgb(255, 226, 229, 234)
    };
}

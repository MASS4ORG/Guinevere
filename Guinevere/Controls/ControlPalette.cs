namespace Guinevere;

/// <summary>
/// Unified fallback colors for built-in controls combining semantic design tokens
/// and component-specific overrides.
/// </summary>
public sealed class ControlPalette
{
    /// <summary>
    /// Creates a palette by applying semantic color declarations from a resolved style over a fallback.
    /// Supported names mirror the property names in kebab case, such as <c>surface-hover</c>,
    /// <c>text-disabled</c>, <c>focus-ring</c>, and <c>text-selection</c>.
    /// </summary>
    public static ControlPalette FromStyle(ResolvedStyle style, ControlPalette? fallback = null)
    {
        ArgumentNullException.ThrowIfNull(style);
        var source = fallback ?? Light;
        Color Get(string name, Color value) => style.GetColor(name) ?? value;
        return new ControlPalette
        {
            BaseBackground = Get("base-background", source.BaseBackground),
            Surface = Get("surface", source.Surface),
            SurfaceHover = Get("surface-hover", source.SurfaceHover),
            SurfaceActive = Get("surface-active", source.SurfaceActive),
            Popup = Get("popup", source.Popup),
            Border = Get("border", source.Border),
            BorderActive = Get("border-active", source.BorderActive),
            Divider = Get("divider", source.Divider),
            Accent = Get("accent", source.Accent),
            AccentHover = Get("accent-hover", source.AccentHover),
            AccentSubtle = Get("accent-subtle", source.AccentSubtle),
            Text = Get("text", source.Text),
            TextDim = Get("text-dim", source.TextDim),
            TextDisabled = Get("text-disabled", source.TextDisabled),
            TextOnAccent = Get("text-on-accent", source.TextOnAccent),
            Selected = Get("selected", source.Selected),
            Positive = Get("positive", source.Positive),
            Negative = Get("negative", source.Negative),
            Warning = Get("warning", source.Warning),
            Info = Get("info", source.Info),
            FocusRing = Get("focus-ring", source.FocusRing),
            Shadow = Get("shadow", source.Shadow),
            Overlay = Get("overlay", source.Overlay),
            TextSelection = Get("text-selection", source.TextSelection)
        };
    }

    /// <summary>The lowest level background (app window, main canvas).</summary>
    public Color BaseBackground { get; init; } = Color.FromArgb(255, 248, 248, 248);

    /// <summary>Fill of an input, a dropdown button or an unchecked box.</summary>
    public Color Surface { get; init; } = Color.White;

    /// <summary>Fill of a surface under the pointer.</summary>
    public Color SurfaceHover { get; init; } = Color.FromArgb(255, 248, 248, 248);

    /// <summary>Fill of an active or pressed surface.</summary>
    public Color SurfaceActive { get; init; } = Color.FromArgb(255, 240, 240, 240);

    /// <summary>Fill of a list or popup opened by a control. Often elevated.</summary>
    public Color Popup { get; init; } = Color.White;

    /// <summary>Outline of a control at rest.</summary>
    public Color Border { get; init; } = Color.Gray;

    /// <summary>Outline of a focused or hovered control.</summary>
    public Color BorderActive { get; init; } = Color.DarkGray;

    /// <summary>Subtle lines separating list items or sections.</summary>
    public Color Divider { get; init; } = Color.FromArgb(255, 224, 224, 224);

    /// <summary>Outline and ring of a focused control, and main brand color.</summary>
    public Color Accent { get; init; } = Color.FromArgb(255, 100, 149, 237);

    /// <summary>Hover state for primary interactive elements.</summary>
    public Color AccentHover { get; init; } = Color.FromArgb(255, 80, 129, 217);

    /// <summary>Light background tint for active states or selections.</summary>
    public Color AccentSubtle { get; init; } = Color.FromArgb(255, 231, 241, 255);

    /// <summary>Main text.</summary>
    public Color Text { get; init; } = Color.Black;

    /// <summary>Placeholder and secondary text.</summary>
    public Color TextDim { get; init; } = Color.Gray;

    /// <summary>Text for inactive form fields and disabled buttons.</summary>
    public Color TextDisabled { get; init; } = Color.FromArgb(255, 173, 181, 189);

    /// <summary>Text that sits on top of the primary accent color.</summary>
    public Color TextOnAccent { get; init; } = Color.White;

    /// <summary>Fill of a checked box, an on toggle or a selected row.</summary>
    public Color Selected { get; init; } = Color.FromArgb(255, 100, 149, 237);

    /// <summary>Outline of a control that would accept what is being dragged over it.</summary>
    public Color Positive { get; init; } = Color.FromArgb(255, 60, 158, 94);

    /// <summary>Outline of a control that would refuse what is being dragged over it.</summary>
    public Color Negative { get; init; } = Color.FromArgb(255, 200, 84, 84);

    /// <summary>Alerts, non-blocking errors, pending states.</summary>
    public Color Warning { get; init; } = Color.FromArgb(255, 245, 158, 11);

    /// <summary>Neutral informational highlights and badges.</summary>
    public Color Info { get; init; } = Color.FromArgb(255, 13, 202, 240);

    /// <summary>Translucent focus halo drawn outside keyboard-focused controls.</summary>
    public Color FocusRing { get; init; } = Color.FromArgb(128, 100, 149, 237);

    /// <summary>Soft shadow used under raised handles and controls.</summary>
    public Color Shadow { get; init; } = Color.FromArgb(100, 0, 0, 0);

    /// <summary>Dimming layer behind modal surfaces.</summary>
    public Color Overlay { get; init; } = Color.FromArgb(140, 0, 0, 0);

    /// <summary>Translucent text-selection highlight.</summary>
    public Color TextSelection { get; init; } = Color.FromArgb(110, 100, 149, 237);

    // ------------------------------------------------------------------------
    // 1. Light
    // ------------------------------------------------------------------------
    /// <summary>The default light palette, matching the built-in controls' historical colors.</summary>
    public static ControlPalette Light { get; } = new();

    // ------------------------------------------------------------------------
    // 2. Dark
    // ------------------------------------------------------------------------
    /// <summary>A dark palette for editor-style hosts.</summary>
    public static ControlPalette Dark { get; } = new()
    {
        BaseBackground = Color.FromArgb(255, 24, 27, 32),
        Surface = Color.FromArgb(255, 28, 31, 37),
        SurfaceHover = Color.FromArgb(255, 38, 42, 50),
        SurfaceActive = Color.FromArgb(255, 51, 56, 66),
        Popup = Color.FromArgb(255, 32, 36, 43),
        Border = Color.FromArgb(255, 51, 56, 66),
        BorderActive = Color.FromArgb(255, 78, 85, 98),
        Divider = Color.FromArgb(255, 60, 66, 78),
        Accent = Color.FromArgb(255, 84, 143, 224),
        AccentHover = Color.FromArgb(255, 104, 163, 244),
        AccentSubtle = Color.FromArgb(255, 38, 42, 50),
        Text = Color.FromArgb(255, 215, 218, 224),
        TextDim = Color.FromArgb(255, 139, 146, 156),
        TextDisabled = Color.FromArgb(255, 96, 104, 118),
        Selected = Color.FromArgb(255, 84, 143, 224),
        Positive = Color.FromArgb(255, 88, 176, 116),
        Negative = Color.FromArgb(255, 208, 102, 102),
        FocusRing = Color.FromArgb(150, 84, 143, 224),
        TextSelection = Color.FromArgb(120, 84, 143, 224)
    };

    // ------------------------------------------------------------------------
    // 3. Mono Light (VS Code / GitHub Light style)
    // ------------------------------------------------------------------------
    /// <summary>A light palette for editor-style hosts.</summary>
    public static ControlPalette MonoLight { get; } = new()
    {
        BaseBackground = Color.FromArgb(255, 244, 244, 246),
        Surface = Color.FromArgb(255, 255, 255, 255),
        SurfaceHover = Color.FromArgb(255, 234, 236, 239),
        SurfaceActive = Color.FromArgb(255, 225, 228, 232),
        Popup = Color.FromArgb(255, 255, 255, 255),
        Border = Color.FromArgb(255, 208, 215, 222),
        BorderActive = Color.FromArgb(255, 140, 149, 159),
        Divider = Color.FromArgb(255, 235, 239, 239),
        Accent = Color.FromArgb(255, 36, 41, 47),
        AccentHover = Color.FromArgb(255, 50, 56, 62),
        AccentSubtle = Color.FromArgb(255, 234, 236, 239),
        Text = Color.FromArgb(255, 31, 35, 40),
        TextDim = Color.FromArgb(255, 101, 109, 118),
        TextDisabled = Color.FromArgb(255, 140, 149, 159),
        TextOnAccent = Color.FromArgb(255, 255, 255, 255),
        Selected = Color.FromArgb(255, 225, 228, 232),
        Positive = Color.FromArgb(255, 26, 127, 55),
        Negative = Color.FromArgb(255, 207, 34, 46),
        FocusRing = Color.FromArgb(145, 36, 41, 47),
        TextSelection = Color.FromArgb(100, 36, 41, 47)
    };

    // ------------------------------------------------------------------------
    // 4. Mono Dark
    // ------------------------------------------------------------------------
    /// <summary>A dark palette for editor-style hosts.</summary>
    public static ControlPalette MonoDark { get; } = new()
    {
        BaseBackground = Color.FromArgb(255, 24, 26, 31),
        Surface = Color.FromArgb(255, 30, 34, 42),
        SurfaceHover = Color.FromArgb(255, 37, 43, 53),
        SurfaceActive = Color.FromArgb(255, 44, 50, 61),
        Popup = Color.FromArgb(255, 37, 43, 53),
        Border = Color.FromArgb(255, 45, 51, 63),
        BorderActive = Color.FromArgb(255, 76, 86, 106),
        Divider = Color.FromArgb(255, 35, 39, 49),
        Accent = Color.FromArgb(255, 157, 165, 180),
        AccentHover = Color.FromArgb(255, 187, 195, 210),
        AccentSubtle = Color.FromArgb(255, 40, 46, 57),
        Text = Color.FromArgb(255, 215, 218, 224),
        TextDim = Color.FromArgb(255, 111, 120, 138),
        TextDisabled = Color.FromArgb(255, 75, 82, 99),
        TextOnAccent = Color.FromArgb(255, 215, 218, 224),
        Selected = Color.FromArgb(255, 40, 46, 57),
        Positive = Color.FromArgb(255, 80, 161, 79),
        Negative = Color.FromArgb(255, 224, 108, 117),
        FocusRing = Color.FromArgb(150, 157, 165, 180),
        TextSelection = Color.FromArgb(110, 157, 165, 180)
    };
}

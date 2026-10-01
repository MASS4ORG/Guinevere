#pragma warning disable CS1591

namespace MASS4.Autoformers;

/// <summary>Width of the label column in a form row.</summary>
public readonly struct FormLabelWidth : IStyleToken<float> { public static float Default => 96f; }

/// <summary>Left indent of a nested object's members under its heading.</summary>
public readonly struct FormIndent : IStyleToken<float> { public static float Default => 12f; }

/// <summary>Inner padding of a nested object's compartment.</summary>
public readonly struct FormCompartmentPadding : IStyleToken<float> { public static float Default => 4f; }

/// <summary>How many compartments enclose the current node; set by the renderer, read to alternate fills.</summary>
readonly struct FormNestingDepth : IStyleToken<int> { public static int Default => 0; }

/// <summary>Glyph of an expanded collection or nested-object heading.</summary>
public readonly struct FormCaretOpen : IStyleToken<string> { public static string Default => WidgetIcons.ChevronDown; }

/// <summary>Glyph of a folded collection or nested-object heading.</summary>
public readonly struct FormCaretClosed : IStyleToken<string> { public static string Default => WidgetIcons.ChevronRight; }

/// <summary>
/// The form's look for the current layout node, read from inherited control style tokens so a host maps its
/// own theme onto a scope with <see cref="ControlPalette"/> and <see cref="ControlStyles.Value{TToken,TValue}"/>.
/// </summary>
readonly struct FormStyle(Gui gui)
{
    T Get<TToken, T>() where TToken : IStyleToken<T> => gui.CurrentNodeScope.Get<StyleValue<TToken, T>>().Value;

    public float RowHeight => Get<ControlCompactHeight, float>();
    public float FontSize => Get<ControlCompactFontSize, float>();
    public float LabelWidth => Get<FormLabelWidth, float>();
    public float Indent => Get<FormIndent, float>();
    public string CaretOpen => Get<FormCaretOpen, string>();
    public string CaretClosed => Get<FormCaretClosed, string>();
    public Color Ink => Get<ControlText, Color>();
    public Color InkDim => Get<ControlTextDim, Color>();
    public Color Field => Get<ControlSurface, Color>();
    public Color Border => Get<ControlBorder, Color>();
    public Color Accent => Get<ControlAccent, Color>();
    public Color Divider => Get<ControlDivider, Color>();
    public Color Negative => Get<ControlNegative, Color>();
    public Color Background => Get<ControlBaseBackground, Color>();
    public float CornerRadius => Get<ControlCornerRadius, float>();
    public float CompartmentPadding => Get<FormCompartmentPadding, float>();
    public int Depth => Get<FormNestingDepth, int>();
}

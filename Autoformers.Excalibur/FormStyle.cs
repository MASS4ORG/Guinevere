#pragma warning disable CS1591

namespace Autoformers;

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
/// The form's colors come from its stylesheet contract and inherited style tokens.
/// Layout and interaction metrics retain their per-scope overrides.
/// </summary>
readonly struct FormStyle(Gui gui)
{
    T Get<TToken, T>() where TToken : IStyleToken<T> => gui.CurrentNodeScope.Get<StyleValue<TToken, T>>().Value;

    ResolvedStyle Sheet
    {
        get
        {
            ExcaliburStyles.Ensure(gui);
            return gui.ResolvePart("form");
        }
    }

    public float RowHeight => Get<ControlCompactHeight, float>();
    public float FontSize => Get<ControlCompactFontSize, float>();
    public float LabelWidth => Get<FormLabelWidth, float>();
    public float Indent => Get<FormIndent, float>();
    public string CaretOpen => Get<FormCaretOpen, string>();
    public string CaretClosed => Get<FormCaretClosed, string>();
    public Color Ink => Sheet.GetColor("color") ?? Color.Black;
    public Color InkDim => Sheet.GetColor("dim-color") ?? Color.Gray;
    public Color Field => Sheet.GetColor("background-color") ?? Color.White;
    public Color Border => Sheet.GetColor("border-color") ?? Color.Gray;
    public Color Accent => Sheet.GetColor("accent-color") ?? Color.CornflowerBlue;
    public Color Divider => Sheet.GetColor("divider-color") ?? Color.LightGray;
    public Color Negative => Sheet.GetColor("negative-color") ?? Color.Red;
    public Color Background => Sheet.GetColor("base-background") ?? Color.FromArgb(242, 242, 242);
    public float CornerRadius => Sheet.GetLength("border-radius") ?? 4f;
    public float CompartmentPadding => Get<FormCompartmentPadding, float>();
    public int Depth => Get<FormNestingDepth, int>();
}

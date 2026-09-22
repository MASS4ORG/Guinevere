#pragma warning disable CS1591

namespace Guinevere;

/// <summary>Describes a strongly typed style token and its fallback value.</summary>
public interface IStyleToken<T>
{
    static abstract T Default { get; }
}

/// <summary>An independently cascading value for a style token.</summary>
public sealed record StyleValue<TToken, TValue>(TValue Value) :
    ILayoutNodeScopeValue<StyleValue<TToken, TValue>> where TToken : IStyleToken<TValue>
{
    public static StyleValue<TToken, TValue> Default { get; } = new(TToken.Default);
}

public readonly struct ControlBaseBackground : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 248, 248, 248); }
public readonly struct ControlSurface : IStyleToken<Color> { public static Color Default => Color.White; }
public readonly struct ControlSurfaceHover : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 248, 248, 248); }
public readonly struct ControlSurfaceActive : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 240, 240, 240); }
public readonly struct ControlPopup : IStyleToken<Color> { public static Color Default => Color.White; }
public readonly struct ControlBorder : IStyleToken<Color> { public static Color Default => Color.Gray; }
public readonly struct ControlBorderActive : IStyleToken<Color> { public static Color Default => Color.DarkGray; }
public readonly struct ControlDivider : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 224, 224, 224); }
public readonly struct ControlAccent : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 100, 149, 237); }
public readonly struct ControlAccentHover : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 80, 129, 217); }
public readonly struct ControlAccentSubtle : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 231, 241, 255); }
public readonly struct ControlText : IStyleToken<Color> { public static Color Default => Color.Black; }
public readonly struct ControlTextDim : IStyleToken<Color> { public static Color Default => Color.Gray; }
public readonly struct ControlTextDisabled : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 173, 181, 189); }
public readonly struct ControlTextOnAccent : IStyleToken<Color> { public static Color Default => Color.White; }
public readonly struct ControlSelected : IStyleToken<Color> { public static Color Default => ControlAccent.Default; }
public readonly struct ControlPositive : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 60, 158, 94); }
public readonly struct ControlNegative : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 200, 84, 84); }
public readonly struct ControlWarning : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 245, 158, 11); }
public readonly struct ControlInfo : IStyleToken<Color> { public static Color Default => Color.FromArgb(255, 13, 202, 240); }
public readonly struct ControlFocusRing : IStyleToken<Color> { public static Color Default => Color.FromArgb(128, 100, 149, 237); }
public readonly struct ControlShadow : IStyleToken<Color> { public static Color Default => Color.FromArgb(100, 0, 0, 0); }
public readonly struct ControlOverlay : IStyleToken<Color> { public static Color Default => Color.FromArgb(140, 0, 0, 0); }
public readonly struct ControlTextSelection : IStyleToken<Color> { public static Color Default => Color.FromArgb(110, 100, 149, 237); }

public readonly struct ControlFieldWidth : IStyleToken<float> { public static float Default => 200f; }
public readonly struct ControlFieldHeight : IStyleToken<float> { public static float Default => 32f; }
public readonly struct ControlCompactHeight : IStyleToken<float> { public static float Default => 24f; }
public readonly struct ControlIndicatorSize : IStyleToken<float> { public static float Default => 20f; }
public readonly struct ControlFontSize : IStyleToken<float> { public static float Default => 14f; }
public readonly struct ControlCompactFontSize : IStyleToken<float> { public static float Default => 12f; }
public readonly struct ControlSpacing : IStyleToken<float> { public static float Default => 8f; }
public readonly struct ControlComfortableSpacing : IStyleToken<float> { public static float Default => 12f; }
public readonly struct ControlCornerRadius : IStyleToken<float> { public static float Default => 4f; }
public readonly struct ControlPanelRadius : IStyleToken<float> { public static float Default => 8f; }

/// <summary>Factories for independently applicable built-in control style values.</summary>
public static class ControlStyles
{
    public static StyleValue<TToken, TValue> Value<TToken, TValue>(TValue value)
        where TToken : IStyleToken<TValue> => new(value);
}

/// <summary>Reads control style values inherited by the current layout node.</summary>
public readonly struct ControlStyleValues(Gui gui)
{
    TValue Get<TToken, TValue>() where TToken : IStyleToken<TValue> =>
        gui.CurrentNodeScope.Get<StyleValue<TToken, TValue>>().Value;

    public Color BaseBackground => Get<ControlBaseBackground, Color>();
    public Color Surface => Get<ControlSurface, Color>();
    public Color SurfaceHover => Get<ControlSurfaceHover, Color>();
    public Color SurfaceActive => Get<ControlSurfaceActive, Color>();
    public Color Popup => Get<ControlPopup, Color>();
    public Color Border => Get<ControlBorder, Color>();
    public Color BorderActive => Get<ControlBorderActive, Color>();
    public Color Divider => Get<ControlDivider, Color>();
    public Color Accent => Get<ControlAccent, Color>();
    public Color AccentHover => Get<ControlAccentHover, Color>();
    public Color AccentSubtle => Get<ControlAccentSubtle, Color>();
    public Color Text => Get<ControlText, Color>();
    public Color TextDim => Get<ControlTextDim, Color>();
    public Color TextDisabled => Get<ControlTextDisabled, Color>();
    public Color TextOnAccent => Get<ControlTextOnAccent, Color>();
    public Color Selected => Get<ControlSelected, Color>();
    public Color Positive => Get<ControlPositive, Color>();
    public Color Negative => Get<ControlNegative, Color>();
    public Color Warning => Get<ControlWarning, Color>();
    public Color Info => Get<ControlInfo, Color>();
    public Color FocusRing => Get<ControlFocusRing, Color>();
    public Color Shadow => Get<ControlShadow, Color>();
    public Color Overlay => Get<ControlOverlay, Color>();
    public Color TextSelection => Get<ControlTextSelection, Color>();
    public float FieldWidth => Get<ControlFieldWidth, float>();
    public float FieldHeight => Get<ControlFieldHeight, float>();
    public float CompactHeight => Get<ControlCompactHeight, float>();
    public float IndicatorSize => Get<ControlIndicatorSize, float>();
    public float FontSize => Get<ControlFontSize, float>();
    public float CompactFontSize => Get<ControlCompactFontSize, float>();
    public float Spacing => Get<ControlSpacing, float>();
    public float ComfortableSpacing => Get<ControlComfortableSpacing, float>();
    public float CornerRadius => Get<ControlCornerRadius, float>();
    public float PanelRadius => Get<ControlPanelRadius, float>();

    public float FieldWidthOr(float value) => value == ControlMetrics.FieldWidth ? FieldWidth : value;
    public float FieldHeightOr(float value) => value == ControlMetrics.FieldHeight ? FieldHeight : value;
    public float CompactHeightOr(float value) => value == ControlMetrics.CompactHeight ? CompactHeight : value;
    public float IndicatorSizeOr(float value) => value == ControlMetrics.IndicatorSize ? IndicatorSize : value;
    public float FontSizeOr(float value) => value == ControlMetrics.FontSize ? FontSize : value;
    public float CompactFontSizeOr(float value) =>
        value == ControlMetrics.CompactFontSize ? CompactFontSize : value;
    public float SpacingOr(float value) => value == ControlMetrics.Spacing ? Spacing : value;
    public float ComfortableSpacingOr(float value) =>
        value == ControlMetrics.ComfortableSpacing ? ComfortableSpacing : value;
    public float CornerRadiusOr(float value) => value == ControlMetrics.CornerRadius ? CornerRadius : value;
    public float PanelRadiusOr(float value) => value == ControlMetrics.PanelRadius ? PanelRadius : value;
}

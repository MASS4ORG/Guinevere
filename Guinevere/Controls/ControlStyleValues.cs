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

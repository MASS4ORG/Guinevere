namespace Guinevere;

/// <summary>Shared default dimensions used by built-in and Excalibur controls.</summary>
public static class ControlMetrics
{
    static readonly ILayoutNodeScopeValue[] Defaults =
    [
        ControlStyles.Value<ControlFieldWidth, float>(FieldWidth),
        ControlStyles.Value<ControlFieldHeight, float>(FieldHeight),
        ControlStyles.Value<ControlCompactHeight, float>(CompactHeight),
        ControlStyles.Value<ControlIndicatorSize, float>(IndicatorSize),
        ControlStyles.Value<ControlFontSize, float>(FontSize),
        ControlStyles.Value<ControlCompactFontSize, float>(CompactFontSize),
        ControlStyles.Value<ControlSpacing, float>(Spacing),
        ControlStyles.Value<ControlComfortableSpacing, float>(ComfortableSpacing),
        ControlStyles.Value<ControlCornerRadius, float>(CornerRadius),
        ControlStyles.Value<ControlPanelRadius, float>(PanelRadius)
    ];

    /// <summary>The default dimensions as independently applicable scope values.</summary>
    public static IReadOnlyList<ILayoutNodeScopeValue> Values => Defaults;

    /// <summary>Applies all default dimensions to a layout scope.</summary>
    public static void Apply(LayoutNodeScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        scope.Set((IEnumerable<ILayoutNodeScopeValue>)Defaults);
    }

    /// <summary>Default width of fields, sliders, and dropdowns.</summary>
    public const float FieldWidth = 200f;
    /// <summary>Default height of text fields and dropdowns.</summary>
    public const float FieldHeight = 32f;
    /// <summary>Default height of compact controls.</summary>
    public const float CompactHeight = 24f;
    /// <summary>Default checkbox and radio indicator size.</summary>
    public const float IndicatorSize = 20f;
    /// <summary>Default body text size.</summary>
    public const float FontSize = 14f;
    /// <summary>Default compact-label text size.</summary>
    public const float CompactFontSize = 12f;
    /// <summary>Default spacing between related control parts.</summary>
    public const float Spacing = 8f;
    /// <summary>Roomier padding used by menus and dialogs.</summary>
    public const float ComfortableSpacing = 12f;
    /// <summary>Default control corner radius.</summary>
    public const float CornerRadius = 4f;
    /// <summary>Default radius for elevated panels and dialogs.</summary>
    public const float PanelRadius = 8f;
}

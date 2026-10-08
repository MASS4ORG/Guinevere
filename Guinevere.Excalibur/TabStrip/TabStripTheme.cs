namespace Guinevere;

/// <summary>
/// Metrics a <c>TabStrip</c> draws with. Colors come from the <c>tabstrip</c>, <c>tab</c>, <c>tab-close</c> and
/// <c>tab-nav</c> rules of the GUI's sheets.
/// </summary>
public sealed class TabStripTheme
{
    /// <summary>Height of the strip and its tabs.</summary>
    public float Height { get; init; } = 26f;

    /// <summary>Label size.</summary>
    public float FontSize { get; init; } = 12f;

    /// <summary>Square reserved for a tab's icon.</summary>
    public float IconSize { get; init; } = 12f;

    /// <summary>The default metrics, matching the dock space.</summary>
    public static TabStripTheme Default { get; } = new();
}

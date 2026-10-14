namespace Guinevere;

/// <summary>Independent skins drawn in background, ghost and foreground order over one destination.</summary>
public sealed record FillLayers
{
    /// <summary>An optional background drawn at full coverage, independent of input mapping.</summary>
    public FillOptions? Background { get; init; }

    /// <summary>An optional ghost skin, drawn when a ghost value is supplied.</summary>
    public FillOptions? Ghost { get; init; }

    /// <summary>The foreground fill skin; null uses the default fill.</summary>
    public FillOptions? Fill { get; init; }
}

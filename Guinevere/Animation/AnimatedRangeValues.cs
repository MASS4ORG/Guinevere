namespace Guinevere;

/// <summary>The foreground and delayed ghost values sampled for one frame, in the host's original units.</summary>
/// <param name="Value">The optionally animated foreground value.</param>
/// <param name="Ghost">The delayed trailing value, never below the foreground.</param>
public readonly record struct AnimatedRangeValues(float Value, float Ghost);

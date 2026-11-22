using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

/// <summary>Weather states displayed by the enum editor.</summary>
public enum Weather
{
    /// <summary>Clear weather.</summary>
    [EnumLabel("Clear skies")] Clear,
    /// <summary>Light rain.</summary>
    [EnumLabel("Light rain")] Drizzle,
    /// <summary>A thunderstorm.</summary>
    [EnumLabel("Thunderstorm!")] Storm,
}

// [EnumLabel] gives enum values friendlier names in the dropdown.
/// <summary>Demonstrates friendly labels for enum choices.</summary>
public sealed class EnumLabelDemo
{
    /// <summary>Forecast selected for the example.</summary>
    public Weather Forecast { get; set; } = Weather.Drizzle;
}

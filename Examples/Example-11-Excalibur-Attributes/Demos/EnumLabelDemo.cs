using MASS4.Attributes;

namespace Example_11_Excalibur_Attributes;

public enum Weather
{
    [EnumLabel("Clear skies")] Clear,
    [EnumLabel("Light rain")] Drizzle,
    [EnumLabel("Thunderstorm!")] Storm,
}

// [EnumLabel] gives enum values friendlier names in the dropdown.
public sealed class EnumLabelDemo
{
    public Weather Forecast { get; set; } = Weather.Drizzle;
}

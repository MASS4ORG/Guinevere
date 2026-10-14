namespace Build;

/// <summary>Names the supported build configurations.</summary>
[TypeConverter(typeof(TypeConverter<ConfigurationOptions>))]
public class ConfigurationOptions : Enumeration
{
    /// <summary>Builds with debug information and assertions.</summary>
    public static ConfigurationOptions Debug { get; set; } = new() { Value = nameof(Debug) };
    /// <summary>Builds optimized release artifacts.</summary>
    public static ConfigurationOptions Release { get; set; } = new() { Value = nameof(Release) };

    /// <summary>Returns the configuration name used by build tools.</summary>
    public static implicit operator string(ConfigurationOptions configuration) => configuration?.Value;
}

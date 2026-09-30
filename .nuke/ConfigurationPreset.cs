namespace Build;

[TypeConverter(typeof(TypeConverter<ConfigurationPreset>))]
public class ConfigurationPreset : Enumeration
{
    public static ConfigurationPreset Debug { get; set; } = new() { Value = nameof(Debug) };
    public static ConfigurationPreset Release { get; set; } = new() { Value = nameof(Release) };

    public static implicit operator string(ConfigurationPreset configuration) => configuration?.Value;
}

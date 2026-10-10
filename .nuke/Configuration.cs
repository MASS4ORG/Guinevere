namespace Build;

[TypeConverter(typeof(TypeConverter<ConfigurationOptions>))]
public class ConfigurationOptions : Enumeration
{
    public static ConfigurationOptions Debug { get; set; } = new() { Value = nameof(Debug) };
    public static ConfigurationOptions Release { get; set; } = new() { Value = nameof(Release) };

    public static implicit operator string(ConfigurationOptions configuration) => configuration?.Value;
}

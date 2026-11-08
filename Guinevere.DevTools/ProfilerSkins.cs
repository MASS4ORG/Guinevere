namespace Guinevere.DevTools;

/// <summary>Embedded PSS skins; every viewer selector can be replaced by a consumer stylesheet.</summary>
public static class ProfilerSkins
{
    /// <summary>Dark stylesheet.</summary>
    public static StyleSheet Dark { get; } = Load("dark");
    /// <summary>Light stylesheet.</summary>
    public static StyleSheet Light { get; } = Load("light");

    static StyleSheet Load(string name)
    {
        using var stream = typeof(ProfilerSkins).Assembly.GetManifestResourceStream(
            $"Guinevere.DevTools.Styles.{name}.pss")!;
        using var reader = new StreamReader(stream);
        return StyleSheet.Parse(reader.ReadToEnd(), new StyleSheetOptions { SourceName = $"profiler-{name}.pss" });
    }
}

namespace Example02;

/// <summary>Standalone PSS sheets shipped by this application, using the public graph selectors.</summary>
public static class GraphDemoStyles
{
    /// <summary>The style names shown by the selector.</summary>
    public static IReadOnlyList<string> Names { get; } = ["Blueprint", "Paper", "High contrast"];
    /// <summary>The three sheets, with no corresponding graph-code palettes.</summary>
    public static IReadOnlyList<StyleSheet> Sheets { get; } =
        new[] { "blueprint", "paper", "high-contrast" }.Select(Load).ToArray();

    static StyleSheet Load(string name)
    {
        using var stream = typeof(GraphDemoStyles).Assembly.GetManifestResourceStream($"Example02.Styles.{name}.pss")!;
        using var reader = new StreamReader(stream);
        return StyleSheet.Parse(reader.ReadToEnd());
    }
}

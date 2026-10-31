namespace Guinevere.Graphs;

/// <summary>The graph PSS base sheet and its default geometry, independent of Excalibur.</summary>
public static class GraphStyles
{
    /// <summary>The base rules, inserted below application sheets on first draw.</summary>
    public static StyleSheet Default { get; } = Load();

    /// <summary>Installs graph defaults beneath application sheets without replacing their rules or tokens.</summary>
    public static void Ensure(Gui gui)
    {
        if (!gui.StyleSheets.Contains(Default))
        {
            gui.StyleSheets.Insert(0, Default);
        }
    }

    internal static ResolvedStyle Defaults(string type) =>
        StyleResolver.Resolve([Default], new StyleTarget(type, null, []));

    internal static float Length(ResolvedStyle style, string property) => style.GetLength(property) ?? 0;
    internal static Color Color(ResolvedStyle style, string property) => style.GetColor(property) ?? Guinevere.Color.Transparent;

    internal static float[]? Dashes(ResolvedStyle style)
    {
        var parts = style.Get("stroke-dasharray")?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length < 2)
        {
            return null;
        }
        var values = new float[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!DashLength(parts[i], out values[i]))
            {
                return null;
            }
        }
        return values.Sum() > 0 ? values : null;
    }

    static bool DashLength(string text, out float value) => StyleValue.TryLength(text, out value, out var percent)
        && !percent && float.IsFinite(value) && value >= 0;

    static StyleSheet Load()
    {
        using var stream = typeof(GraphStyles).Assembly.GetManifestResourceStream("Guinevere.Graphs.Styles.graph.pss")!;
        using var reader = new StreamReader(stream);
        return StyleSheet.Parse(reader.ReadToEnd());
    }
}

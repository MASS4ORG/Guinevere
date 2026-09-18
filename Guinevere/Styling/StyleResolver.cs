namespace Guinevere;

/// <summary>The declarations that apply to one element after the cascade, with typed accessors.</summary>
public sealed class ResolvedStyle
{
    /// <summary>An empty resolved style — no declarations.</summary>
    public static readonly ResolvedStyle Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

    readonly IReadOnlyDictionary<string, string> _declarations;

    internal ResolvedStyle(IReadOnlyDictionary<string, string> declarations) => _declarations = declarations;

    /// <summary>Every resolved property → value.</summary>
    public IReadOnlyDictionary<string, string> Declarations => _declarations;

    /// <summary>Whether a property was set.</summary>
    /// <param name="property">The property name.</param>
    public bool Has(string property) => _declarations.ContainsKey(property);

    /// <summary>The raw string value of a property, or <c>null</c>.</summary>
    /// <param name="property">The property name.</param>
    public string? Get(string property) => _declarations.GetValueOrDefault(property);

    /// <summary>The value of a property parsed as a color, or <c>null</c>.</summary>
    /// <param name="property">The property name.</param>
    public Color? GetColor(string property) =>
        _declarations.TryGetValue(property, out var v) && StyleValue.TryColor(v, out var c) ? c : null;

    /// <summary>The value of a property parsed as a pixel length, or <c>null</c>.</summary>
    /// <param name="property">The property name.</param>
    public float? GetLength(string property) =>
        _declarations.TryGetValue(property, out var v) && StyleValue.TryLength(v, out var f, out _) ? f : null;
}

/// <summary>Runs the <c>.uss</c> cascade: matches rules against an element and merges the winners.</summary>
public static class StyleResolver
{
    /// <summary>
    /// Resolves the effective style for <paramref name="target"/> across <paramref name="sheets"/>
    /// (applied in order). Within and across sheets, higher <see cref="Selector.Specificity"/> wins;
    /// ties break by sheet order then rule order.
    /// </summary>
    /// <param name="sheets">Stylesheets to apply, lowest priority first.</param>
    /// <param name="target">The element being styled.</param>
    public static ResolvedStyle Resolve(IReadOnlyList<StyleSheet> sheets, in StyleTarget target)
    {
        if (sheets.Count == 0) return ResolvedStyle.Empty;

        var matches = new List<(int Specificity, int Sheet, int Order, StyleRule Rule, StyleSheet Owner)>();
        for (var s = 0; s < sheets.Count; s++)
        {
            foreach (var rule in sheets[s].Rules)
            {
                var best = -1;
                foreach (var selector in rule.Selectors)
                    if (selector.Matches(target) && selector.Specificity > best)
                        best = selector.Specificity;

                if (best >= 0)
                    matches.Add((best, s, rule.Order, rule, sheets[s]));
            }
        }

        if (matches.Count == 0) return ResolvedStyle.Empty;

        matches.Sort(static (a, b) =>
        {
            var c = a.Specificity.CompareTo(b.Specificity);
            if (c != 0) return c;
            c = a.Sheet.CompareTo(b.Sheet);
            return c != 0 ? c : a.Order.CompareTo(b.Order);
        });

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, _, _, rule, owner) in matches)
            foreach (var (prop, value) in rule.Declarations)
                merged[prop] = owner.ExpandVariables(value);

        return new ResolvedStyle(merged);
    }
}

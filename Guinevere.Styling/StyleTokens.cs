namespace Guinevere;

/// <summary>
/// Style tokens a node passes down to its subtree, like inherited CSS custom properties: the rule-scoped
/// <c>$token = value;</c> declarations of its matched rules, and values set with <c>gui.SetStyleToken</c>. Every styled
/// descendant reads them above the sheets' and host tokens and below its own rules' tokens.
/// </summary>
public sealed class StyleTokens : ILayoutNodeScopeValue<StyleTokens>
{
    StyleTokens(Dictionary<string, string> values) => Values = values;

    /// <summary>No inherited tokens.</summary>
    public static StyleTokens Default { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>Token declaration texts keyed as <c>--name</c>.</summary>
    public IReadOnlyDictionary<string, string> Values { get; }

    /// <summary>These tokens with <paramref name="overrides"/> applied; the same instance when there is nothing to add.</summary>
    internal StyleTokens With(IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null || overrides.Count == 0) return this;
        var merged = new Dictionary<string, string>((IDictionary<string, string>)Values, StringComparer.Ordinal);
        foreach (var (name, value) in overrides) merged[name] = value;
        return new StyleTokens(merged);
    }
}

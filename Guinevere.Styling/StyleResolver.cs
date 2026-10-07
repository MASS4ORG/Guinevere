namespace Guinevere;

/// <summary>A typed value supplied to a styled node by immediate-mode code.</summary>
public readonly record struct StyleVariable(string Name, object Value);

/// <summary>The declarations that apply to one element after the cascade, with typed accessors.</summary>
public sealed class ResolvedStyle
{
    /// <summary>An empty resolved style — no declarations.</summary>
    public static readonly ResolvedStyle Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

    readonly Dictionary<string, string> _declarations;

    internal ResolvedStyle(Dictionary<string, string> declarations, Dictionary<string, Uri>? bases = null)
    {
        _declarations = declarations;
        Bases = bases;
    }

    internal Dictionary<string, string> DeclarationMap => _declarations;

    internal Dictionary<string, Uri>? Bases { get; }

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

    /// <summary>
    /// The value of a property parsed as <c>url()</c>, resolved against the base of the sheet that declared it,
    /// or <c>null</c>.
    /// </summary>
    /// <param name="property">The property name.</param>
    public Uri? GetUrl(string property) =>
        _declarations.TryGetValue(property, out var v)
        && StyleValue.TryUrl(v, Bases?.GetValueOrDefault(property), out var uri)
            ? uri
            : null;
}

/// <summary>
/// The cascade result for one target before call-site variables: the expanded style, the raw values that reference
/// variables, and the rule-scoped variables they may need.
/// </summary>
sealed record StyleCacheEntry(
    ResolvedStyle Style,
    Dictionary<string, string>? Referencing,
    Dictionary<string, string>? Locals)
{
    public static readonly StyleCacheEntry Empty = new(ResolvedStyle.Empty, null, null);
}

/// <summary>Runs the <c>.pss</c> cascade: matches rules against an element and merges the winners.</summary>
public static class StyleResolver
{
    enum AliasUse { Properties, Selector }

    /// <summary>
    /// Resolves the effective style for <paramref name="target"/> across <paramref name="sheets"/>
    /// (applied in order). Within and across sheets, higher <see cref="Selector.Specificity"/> wins;
    /// ties break by sheet order then rule order. A <see cref="StyleSheetCollection"/> resolves through its cache
    /// and host tokens.
    /// </summary>
    /// <param name="sheets">Stylesheets to apply, lowest priority first.</param>
    /// <param name="target">The element being styled.</param>
    /// <param name="scopedVariables">Call-site variables, which override stylesheet variables.</param>
    public static ResolvedStyle Resolve(IReadOnlyList<StyleSheet> sheets, in StyleTarget target,
        IReadOnlyList<StyleVariable>? scopedVariables = null)
    {
        if (sheets is StyleSheetCollection collection) return collection.Resolve(target, scopedVariables);
        if (sheets.Count == 0) return ResolvedStyle.Empty;
        var globals = LayerVariables(sheets, null);
        return ApplyScoped(ResolveEntry(sheets, target, globals), scopedVariables, globals);
    }

    /// <summary>Top-level tokens of every sheet in order (later sheets win), then host tokens above all sheets.</summary>
    internal static Dictionary<string, string> LayerVariables(IReadOnlyList<StyleSheet> sheets,
        IReadOnlyDictionary<string, string>? hostTokens)
    {
        var layered = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var s = 0; s < sheets.Count; s++)
            foreach (var (name, value) in sheets[s].Variables) layered[name] = value;
        if (hostTokens is not null)
            foreach (var (name, value) in hostTokens) layered[name] = value;
        return layered;
    }

    /// <summary>Matches and merges rules for <paramref name="target"/>, expanding tokens without call-site values.</summary>
    internal static StyleCacheEntry ResolveEntry(IReadOnlyList<StyleSheet> sheets, in StyleTarget target,
        IReadOnlyDictionary<string, string> globals)
    {
        var matches = Match(sheets, target);
        if (matches.Count == 0) return StyleCacheEntry.Empty;
        var (raw, locals) = Merge(matches);
        return Expand(raw, locals, globals);
    }

    /// <summary>Applies matched rules in cascade order: properties with their sheet base, and rule-scoped tokens.</summary>
    static (Dictionary<string, (string Value, Uri? Base)> Raw, Dictionary<string, string>? Locals) Merge(
        List<(int Specificity, int Sheet, int Order, StyleRule Rule)> matches)
    {
        var raw = new Dictionary<string, (string Value, Uri? Base)>(StringComparer.Ordinal);
        Dictionary<string, string>? locals = null;
        foreach (var (_, _, _, rule) in matches)
            foreach (var (prop, value) in rule.Declarations)
                if (prop.StartsWith("--", StringComparison.Ordinal)) (locals ??= new(StringComparer.Ordinal))[prop] = value;
                else raw[prop] = (value, rule.BaseUri);
        return (raw, locals);
    }

    static StyleCacheEntry Expand(Dictionary<string, (string Value, Uri? Base)> raw,
        Dictionary<string, string>? locals, IReadOnlyDictionary<string, string> globals)
    {
        var merged = new Dictionary<string, string>(raw.Count, StringComparer.Ordinal);
        Dictionary<string, Uri>? bases = null;
        Dictionary<string, string>? referencing = null;
        Func<string, string?> lookup = name => locals?.GetValueOrDefault(name) ?? globals.GetValueOrDefault(name);
        foreach (var (prop, (value, baseUri)) in raw)
        {
            merged[prop] = StyleSheet.Expand(value, lookup);
            if (baseUri is not null) (bases ??= new(StringComparer.Ordinal))[prop] = baseUri;
            if (StyleSheet.References(value)) (referencing ??= new(StringComparer.Ordinal))[prop] = value;
        }
        return new StyleCacheEntry(new ResolvedStyle(merged, bases), referencing, locals);
    }

    /// <summary>
    /// Returns the cached style when no call-site variable can change it; otherwise re-expands only the
    /// declarations that reference variables. Precedence: call site, then rule scope, then sheet and host tokens.
    /// </summary>
    internal static ResolvedStyle ApplyScoped(StyleCacheEntry entry, IReadOnlyList<StyleVariable>? scoped,
        IReadOnlyDictionary<string, string> globals) =>
        scoped is null || scoped.Count == 0 || entry.Referencing is null
            ? entry.Style
            : Reexpand(entry, entry.Referencing, scoped, globals);

    static ResolvedStyle Reexpand(StyleCacheEntry entry, Dictionary<string, string> referencing,
        IReadOnlyList<StyleVariable> scoped, IReadOnlyDictionary<string, string> globals)
    {
        var caller = new Dictionary<string, string>(scoped.Count, StringComparer.Ordinal);
        for (var i = 0; i < scoped.Count; i++)
        {
            var variable = scoped[i];
            var name = variable.Name.StartsWith("--", StringComparison.Ordinal) ? variable.Name : $"--{variable.Name}";
            caller[name] = Convert.ToString(variable.Value, System.Globalization.CultureInfo.InvariantCulture)
                           ?? string.Empty;
        }

        var locals = entry.Locals;
        Func<string, string?> lookup = name =>
            caller.GetValueOrDefault(name) ?? locals?.GetValueOrDefault(name) ?? globals.GetValueOrDefault(name);
        var merged = new Dictionary<string, string>(entry.Style.DeclarationMap, StringComparer.Ordinal);
        foreach (var (prop, value) in referencing) merged[prop] = StyleSheet.Expand(value, lookup);
        return new ResolvedStyle(merged, entry.Style.Bases);
    }

    static List<(int Specificity, int Sheet, int Order, StyleRule Rule)> Match(IReadOnlyList<StyleSheet> sheets,
        in StyleTarget target)
    {
        var matches = new List<(int Specificity, int Sheet, int Order, StyleRule Rule)>();
        for (var s = 0; s < sheets.Count; s++)
        {
            var sheet = sheets[s];
            for (var r = 0; r < sheet.Rules.Count; r++)
            {
                var rule = sheet.Rules[r];
                var best = -1;
                for (var k = 0; k < rule.Selectors.Count; k++)
                {
                    var selector = rule.Selectors[k];
                    if (selector.Specificity > best && Matches(selector, target, sheet)) best = selector.Specificity;
                }
                if (best >= 0) matches.Add((best, s, rule.Order, rule));
            }
        }

        matches.Sort(static (a, b) =>
        {
            var c = a.Specificity.CompareTo(b.Specificity);
            if (c != 0) return c;
            c = a.Sheet.CompareTo(b.Sheet);
            return c != 0 ? c : a.Order.CompareTo(b.Order);
        });
        return matches;
    }

    static bool Matches(Selector selector, in StyleTarget target, StyleSheet sheet)
    {
        if (selector.Matches(target)) return true;
        if (!sheet.HasInheritance) return false;
        return MatchesSubjectAlias(selector, target, sheet)
               || selector.HasCombinator && MatchesAncestorAlias(selector, target, sheet);
    }

    static bool MatchesSubjectAlias(Selector selector, in StyleTarget target, StyleSheet sheet)
    {
        var use = selector.HasCombinator ? AliasUse.Selector : AliasUse.Properties;
        foreach (var alias in InheritedNames(target.Type, sheet, use))
            if (selector.Matches(target with { Type = alias })) return true;
        return false;
    }

    static bool MatchesAncestorAlias(Selector selector, in StyleTarget target, StyleSheet sheet)
    {
        if (target.Ancestors is not { } source) return false;
        for (var i = 0; i < source.Count; i++)
            foreach (var alias in InheritedNames(source[i].Type, sheet, AliasUse.Selector))
            {
                var ancestors = source.ToArray();
                ancestors[i] = ancestors[i] with { Type = alias };
                if (selector.Matches(target with { Ancestors = ancestors })) return true;
            }
        return false;
    }

    /// <summary>
    /// Transitive aliases of <paramref name="type"/>. <c>#inherit</c> applies everywhere;
    /// <c>#inherit-properties</c> only to a single-compound selector's subject (copying the parent's own style);
    /// <c>#inherit-selector</c> only inside combinator selectors (matching parent relationships).
    /// </summary>
    static IEnumerable<string> InheritedNames(string? type, StyleSheet sheet, AliasUse use)
    {
        if (type is null) yield break;
        var extra = use == AliasUse.Properties ? sheet.PropertyInheritance : sheet.SelectorInheritance;
        var pending = new Stack<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(type);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            sheet.Inheritance.TryGetValue(current, out var shared);
            extra.TryGetValue(current, out var specific);
            foreach (var parent in (shared ?? []).Concat(specific ?? []))
                if (seen.Add(parent)) { yield return parent; pending.Push(parent); }
        }
    }
}

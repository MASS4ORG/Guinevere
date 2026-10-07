using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>One <c>.pss</c> rule: the selectors it applies to and the declarations it sets.</summary>
public sealed class StyleRule
{
    /// <summary>Selectors this rule's declarations apply to (a comma-separated group).</summary>
    public required IReadOnlyList<Selector> Selectors { get; init; }

    /// <summary>Property → value declarations, in source order (later wins within the rule).</summary>
    public required IReadOnlyDictionary<string, string> Declarations { get; init; }

    /// <summary>0-based position of the rule in its stylesheet, for cascade tie-breaking.</summary>
    public required int Order { get; init; }

    /// <summary>Base for relative <c>url()</c> values, taken from the sheet that declared the rule.</summary>
    internal Uri? BaseUri { get; init; }
}

/// <summary>A <c>@font-face</c> the host should load, with its source resolved against the sheet location.</summary>
/// <param name="Family">The <c>font-family</c> name declarations refer to.</param>
/// <param name="Source">The resolved <c>src</c> location.</param>
public sealed record StyleFontFace(string Family, Uri Source);

/// <summary>Kinds of PanGui constructs that are parsed and kept but not applied yet.</summary>
public enum StyleDeferredKind
{
    /// <summary><c>shape name = …;</c></summary>
    Shape,
    /// <summary><c>effect name { … }</c></summary>
    Effect,
    /// <summary><c>bg-shape = …;</c></summary>
    BackgroundShape,
    /// <summary><c>@mixin name(…) { … }</c></summary>
    Mixin,
    /// <summary><c>@style name(…) = … { … }</c></summary>
    StyleMacro,
    /// <summary><c>@shape name = …;</c></summary>
    ShapeMacro,
    /// <summary><c>@effect name(…) { … }</c></summary>
    EffectMacro,
    /// <summary><c>@name(…);</c> invocation of a mixin or macro.</summary>
    Invocation,
}

/// <summary>A parsed but not yet applied PanGui construct, kept verbatim with its source position.</summary>
/// <param name="Kind">The construct kind.</param>
/// <param name="Text">The construct's source text, with comments blanked.</param>
/// <param name="Line">1-based line of the construct.</param>
/// <param name="Column">1-based column of the construct.</param>
public sealed record StyleDeferredConstruct(StyleDeferredKind Kind, string Text, int Line, int Column);

/// <summary>
/// A parsed <c>.pss</c> (PanGui Style Sheet): rules, <c>$token</c> variables, <c>@const</c> constants,
/// <c>#inherit</c> aliases, <c>@font-face</c> entries and PanGui constructs kept for later application.
/// </summary>
public sealed class StyleSheet
{
    static readonly Regex VarPattern = new(@"var\(\s*(--[A-Za-z0-9_-]+)\s*\)", RegexOptions.Compiled);
    static readonly Regex DollarVarPattern = new(@"\$([A-Za-z_][A-Za-z0-9_-]*)", RegexOptions.Compiled);
    const int MaxExpansionDepth = 8;

    /// <summary>The stylesheet's rules, imported rules first, in source order.</summary>
    public IReadOnlyList<StyleRule> Rules { get; }

    /// <summary>Top-level <c>$name = value;</c> tokens, stored as <c>--name</c>; later sheets override them.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; }

    /// <summary><c>@const name = value;</c> values, readable by the host as sheet metadata.</summary>
    public IReadOnlyDictionary<string, string> Constants { get; }

    /// <summary>Aliases declared with <c>#inherit(...)</c>: copy properties and match parent selectors.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Inheritance { get; }

    /// <summary>Aliases declared with <c>#inherit-properties(...)</c>: copy properties only.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PropertyInheritance { get; }

    /// <summary>Aliases declared with <c>#inherit-selector(...)</c>: match parent selectors only.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SelectorInheritance { get; }

    /// <summary><c>@font-face</c> entries for the host to load; the core does not load fonts itself.</summary>
    public IReadOnlyList<StyleFontFace> FontFaces { get; }

    /// <summary>PanGui shapes, effects, mixins and macros that are parsed but not applied.</summary>
    public IReadOnlyList<StyleDeferredConstruct> Deferred { get; }

    /// <summary>The options the sheet was parsed with.</summary>
    public StyleSheetOptions Options { get; }

    internal bool HasInheritance =>
        Inheritance.Count > 0 || PropertyInheritance.Count > 0 || SelectorInheritance.Count > 0;

    internal StyleSheet(StyleSheetParser parsed)
    {
        Rules = parsed.Rules;
        Variables = parsed.Variables;
        Constants = parsed.Constants;
        Inheritance = parsed.Inheritance;
        PropertyInheritance = parsed.PropertyInheritance;
        SelectorInheritance = parsed.SelectorInheritance;
        FontFaces = parsed.FontFaces;
        Deferred = parsed.Deferred;
        Options = parsed.Options;
    }

    /// <summary>Parses <c>.pss</c> text into a stylesheet.</summary>
    /// <param name="text">The stylesheet source.</param>
    /// <param name="options">Source name, URL base, import resolver and syntax options.</param>
    /// <exception cref="StyleSheetException">The text is malformed; the message carries <c>source:line:column</c>.</exception>
    public static StyleSheet Parse(string text, StyleSheetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return StyleSheetParser.Parse(text, options ?? new StyleSheetOptions(), []);
    }

    /// <summary>Substitutes <c>$name</c> and <c>var(--name)</c> references using this sheet's variables.</summary>
    /// <param name="value">A declaration value that may reference variables.</param>
    /// <param name="scoped">Optional rule or call-site variables that override the sheet's.</param>
    public string ExpandVariables(string value, IReadOnlyDictionary<string, string>? scoped = null) =>
        Expand(value, name => scoped?.GetValueOrDefault(name) ?? Variables.GetValueOrDefault(name));

    internal static bool References(string value) =>
        value.Contains('$') || value.Contains("var(", StringComparison.Ordinal);

    internal static string Expand(string value, Func<string, string?> lookup, int depth = 0)
    {
        if (!References(value)) return value;
        var expanded = VarPattern.Replace(value, m => Substitute(m.Groups[1].Value, m.Value, lookup, depth));
        return DollarVarPattern.Replace(expanded, m => Substitute($"--{m.Groups[1].Value}", m.Value, lookup, depth));
    }

    static string Substitute(string name, string original, Func<string, string?> lookup, int depth)
    {
        var raw = lookup(name);
        if (raw is null) return original;
        return depth < MaxExpansionDepth ? Expand(raw, lookup, depth + 1) : raw;
    }
}

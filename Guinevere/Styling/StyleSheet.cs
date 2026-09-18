using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>One <c>.uss</c> rule: the selectors it applies to and the declarations it sets.</summary>
public sealed class StyleRule
{
    /// <summary>Selectors this rule's declarations apply to (a comma-separated group).</summary>
    public required IReadOnlyList<Selector> Selectors { get; init; }

    /// <summary>Property → value declarations, in source order (later wins within the rule).</summary>
    public required IReadOnlyDictionary<string, string> Declarations { get; init; }

    /// <summary>0-based position of the rule in its stylesheet, for cascade tie-breaking.</summary>
    public required int Order { get; init; }
}

/// <summary>
/// A parsed <c>.uss</c> stylesheet: a flat list of rules plus custom-property variables. Supports
/// type / <c>.class</c> / <c>#id</c> / compound selectors, the <c>:hover</c> / <c>:active</c> /
/// <c>:focus</c> / <c>:disabled</c> modifiers, <c>--name: value;</c> variables and <c>var(--name)</c>,
/// and <c>/* … */</c> comments. Combinators, nesting, mixins and transitions are not supported yet.
/// </summary>
public sealed class StyleSheet
{
    static readonly Regex CommentPattern = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex VarPattern = new(@"var\(\s*(--[A-Za-z0-9_-]+)\s*\)", RegexOptions.Compiled);

    /// <summary>The stylesheet's rules, in source order.</summary>
    public IReadOnlyList<StyleRule> Rules { get; }

    /// <summary>Custom-property variables declared at the top level (<c>--name: value;</c>).</summary>
    public IReadOnlyDictionary<string, string> Variables { get; }

    StyleSheet(IReadOnlyList<StyleRule> rules, IReadOnlyDictionary<string, string> variables)
    {
        Rules = rules;
        Variables = variables;
    }

    /// <summary>Parses <c>.uss</c> text into a stylesheet.</summary>
    /// <param name="css">The stylesheet source.</param>
    /// <exception cref="FormatException">A rule block or selector is malformed.</exception>
    public static StyleSheet Parse(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        var text = CommentPattern.Replace(css, string.Empty);

        var rules = new List<StyleRule>();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = 0;
        var i = 0;

        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ';')) i++;
            if (i >= text.Length) break;

            // A top-level "--name: value;" is a variable, not a rule.
            if (text[i] == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                var semi = text.IndexOf(';', i);
                var end = semi < 0 ? text.Length : semi;
                var decl = text[i..end];
                var colon = decl.IndexOf(':');
                if (colon > 0)
                    variables[decl[..colon].Trim()] = decl[(colon + 1)..].Trim();
                i = end + 1;
                continue;
            }

            var brace = text.IndexOf('{', i);
            if (brace < 0) break;
            var closeBrace = text.IndexOf('}', brace);
            if (closeBrace < 0)
                throw new FormatException("Unterminated rule block: missing '}'");

            var selectorText = text[i..brace].Trim();
            var body = text[(brace + 1)..closeBrace];

            var selectors = selectorText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Selector.Parse)
                .ToArray();

            var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var part in body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = part.IndexOf(':');
                if (colon <= 0) continue;
                declarations[part[..colon].Trim()] = part[(colon + 1)..].Trim();
            }

            rules.Add(new StyleRule { Selectors = selectors, Declarations = declarations, Order = order++ });
            i = closeBrace + 1;
        }

        return new StyleSheet(rules, variables);
    }

    /// <summary>Substitutes <c>var(--name)</c> references in <paramref name="value"/> using this sheet's variables.</summary>
    /// <param name="value">A declaration value that may contain <c>var(…)</c>.</param>
    public string ExpandVariables(string value) =>
        !value.Contains("var(", StringComparison.Ordinal)
            ? value
            : VarPattern.Replace(value, m => Variables.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
}

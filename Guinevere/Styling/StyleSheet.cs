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
/// hierarchy combinators, nested rules, and <c>/* … */</c> comments.
/// </summary>
public sealed class StyleSheet
{
    static readonly Regex CommentPattern = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    static readonly Regex LineCommentPattern = new(@"//.*$", RegexOptions.Multiline | RegexOptions.Compiled);
    static readonly Regex VarPattern = new(@"var\(\s*(--[A-Za-z0-9_-]+)\s*\)", RegexOptions.Compiled);
    static readonly Regex DollarVarPattern = new(@"\$([A-Za-z_][A-Za-z0-9_-]*)", RegexOptions.Compiled);
    static readonly Regex ConstPattern = new(@"@const\s+([A-Za-z_][A-Za-z0-9_-]*)\s*=\s*([^;]+);",
        RegexOptions.Compiled);
    static readonly Regex InheritPattern = new(
        @"^(?<child>[A-Za-z_][A-Za-z0-9_-]*)\s+#inherit\((?<parents>[^)]*)\)$", RegexOptions.Compiled);

    /// <summary>The stylesheet's rules, in source order.</summary>
    public IReadOnlyList<StyleRule> Rules { get; }

    /// <summary>Custom-property variables declared at the top level (<c>--name: value;</c>).</summary>
    public IReadOnlyDictionary<string, string> Variables { get; }
    /// <summary>Selector aliases declared with PanGui-compatible <c>#inherit(...)</c>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Inheritance { get; }

    StyleSheet(IReadOnlyList<StyleRule> rules, IReadOnlyDictionary<string, string> variables,
        IReadOnlyDictionary<string, IReadOnlyList<string>> inheritance)
    {
        Rules = rules;
        Variables = variables;
        Inheritance = inheritance;
    }

    /// <summary>Parses <c>.uss</c> text into a stylesheet.</summary>
    /// <param name="css">The stylesheet source.</param>
    /// <exception cref="FormatException">A rule block or selector is malformed.</exception>
    public static StyleSheet Parse(string css)
    {
        ArgumentNullException.ThrowIfNull(css);
        var text = LineCommentPattern.Replace(CommentPattern.Replace(css, string.Empty), string.Empty);
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        text = ConstPattern.Replace(text, match =>
        {
            constants[match.Groups[1].Value] = match.Groups[2].Value.Trim();
            return string.Empty;
        });
        foreach (var (name, value) in constants)
            text = Regex.Replace(text, $@"@{Regex.Escape(name)}\b", value);
        var rules = new List<StyleRule>();
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        var inheritance = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var order = 0;
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ';')) i++;
            if (i >= text.Length) break;
            if (text[i] == '$' || text[i] == '-' && i + 1 < text.Length && text[i + 1] == '-')
            {
                var semi = text.IndexOf(';', i);
                var end = semi < 0 ? text.Length : semi;
                var decl = text[i..end];
                var separator = DeclarationSeparator(decl);
                if (separator > 0)
                {
                    var name = decl[..separator].Trim();
                    if (name[0] == '$') name = $"--{name[1..]}";
                    variables[name] = decl[(separator + 1)..].Trim();
                }
                i = end + 1;
                continue;
            }
            var brace = text.IndexOf('{', i);
            if (brace < 0) throw Error(text, i, "Expected a rule block");
            var selectorText = text[i..brace].Trim();
            if (selectorText.Length == 0) throw Error(text, i, "Missing selector");
            var inherit = InheritPattern.Match(selectorText);
            if (inherit.Success)
            {
                selectorText = inherit.Groups["child"].Value;
                inheritance[selectorText] = inherit.Groups["parents"].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            var selectors = SplitSelectors(selectorText);
            i = brace + 1;
            ParseBlock(text, ref i, selectors, rules, ref order);
        }
        ValidateInheritance(inheritance);
        return new StyleSheet(rules, variables, inheritance);
    }

    static void ParseBlock(string text, ref int i, string[] selectorTexts, List<StyleRule> rules, ref int order)
    {
        var ownOrder = order++;
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ';')) i++;
            if (i >= text.Length) throw Error(text, i, "Unterminated rule block: missing '}'");
            if (text[i] == '}') { i++; break; }

            var semi = text.IndexOf(';', i);
            var brace = text.IndexOf('{', i);
            var close = text.IndexOf('}', i);
            if (brace >= 0 && (semi < 0 || brace < semi) && (close < 0 || brace < close))
            {
                var nestedText = text[i..brace].Trim();
                var nested = SplitSelectors(nestedText)
                    .SelectMany(child => selectorTexts.Select(parent => Combine(parent, child)))
                    .ToArray();
                i = brace + 1;
                ParseBlock(text, ref i, nested, rules, ref order);
                continue;
            }

            var end = semi >= 0 && (close < 0 || semi < close) ? semi : close;
            if (end < 0) throw Error(text, i, "Unterminated declaration");
            var declaration = text[i..end].Trim();
            var separator = DeclarationSeparator(declaration);
            if (separator <= 0) throw Error(text, i, $"Malformed declaration '{declaration}'");
            var property = declaration[..separator].Trim();
            if (property[0] == '$') property = $"--{property[1..]}";
            declarations[property] = declaration[(separator + 1)..].Trim();
            i = end + (end == close ? 0 : 1);
        }

        if (declarations.Count > 0)
            rules.Add(new StyleRule
            {
                Selectors = selectorTexts.Select(Selector.Parse).ToArray(),
                Declarations = declarations,
                Order = ownOrder,
            });
    }

    static string[] SplitSelectors(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static string Combine(string parent, string child)
    {
        if (child.Contains('&', StringComparison.Ordinal)) return child.Replace("&", parent, StringComparison.Ordinal);
        if (child.StartsWith(':')) return $"{parent}{child}";
        return child.StartsWith('>') ? $"{parent} {child}" : $"{parent} {child}";
    }

    static int DeclarationSeparator(string text)
    {
        var equals = text.IndexOf('=');
        var colon = text.IndexOf(':');
        if (equals < 0) return colon;
        return colon < 0 ? equals : Math.Min(equals, colon);
    }

    static FormatException Error(string text, int offset, string message)
    {
        var line = 1;
        var column = 1;
        for (var j = 0; j < Math.Min(offset, text.Length); j++)
            if (text[j] == '\n') { line++; column = 1; } else column++;
        return new FormatException($"{message} at line {line}, column {column}");
    }

    static void ValidateInheritance(IReadOnlyDictionary<string, IReadOnlyList<string>> inheritance)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in inheritance.Keys) Visit(child);
        return;

        void Visit(string child)
        {
            if (visited.Contains(child)) return;
            if (!visiting.Add(child)) throw new FormatException($"Cyclic style inheritance involving '{child}'");
            if (inheritance.TryGetValue(child, out var parents))
                foreach (var parent in parents) Visit(parent);
            visiting.Remove(child);
            visited.Add(child);
        }
    }

    /// <summary>Substitutes <c>var(--name)</c> references in <paramref name="value"/> using this sheet's variables.</summary>
    /// <param name="value">A declaration value that may contain <c>var(…)</c>.</param>
    /// <param name="scoped">Optional rule or call-site variables that override globals.</param>
    public string ExpandVariables(string value, IReadOnlyDictionary<string, string>? scoped = null)
    {
        var expanded = VarPattern.Replace(value, m => Lookup(m.Groups[1].Value, scoped) ?? m.Value);
        return DollarVarPattern.Replace(expanded, m => Lookup($"--{m.Groups[1].Value}", scoped) ?? m.Value);
    }

    string? Lookup(string name, IReadOnlyDictionary<string, string>? scoped) =>
        scoped?.GetValueOrDefault(name) ?? Variables.GetValueOrDefault(name);
}

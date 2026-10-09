using System.Text.RegularExpressions;

namespace Guinevere;

/// <summary>
/// Single-use <c>.pss</c> parser. Comments, <c>@import</c> and <c>@const</c> are blanked in place before the
/// statement walk, so every error offset maps to the original source.
/// </summary>
sealed class StyleSheetParser
{
    static readonly Regex ConstPattern = new(@"@const\s+([A-Za-z_][A-Za-z0-9_-]*)\s*=\s*([^;]+);",
        RegexOptions.Compiled);
    static readonly Regex ConstReference = new(@"@([A-Za-z_][A-Za-z0-9_-]*)", RegexOptions.Compiled);
    static readonly Regex ImportPattern = new(@"@import\s+(?:""(?<id>[^""]*)""|'(?<id>[^']*)')\s*;",
        RegexOptions.Compiled);
    static readonly Regex InheritPattern = new(
        @"^(?<child>[A-Za-z_][A-Za-z0-9_-]*)\s+#(?<kind>inherit(?:-properties|-selector)?)\((?<parents>[^)]*)\)$",
        RegexOptions.Compiled);

    /// <summary>At-rules handled before the statement walk; reaching one here means its syntax is wrong.</summary>
    static readonly Dictionary<string, string> MalformedAtRules = new(StringComparer.Ordinal)
    {
        ["import"] = "Malformed @import; expected @import \"id\";",
        ["const"] = "Malformed @const; expected @const name = value;",
    };

    static readonly Dictionary<string, StyleDeferredKind> DeferredAtRules = new(StringComparer.Ordinal)
    {
        ["mixin"] = StyleDeferredKind.Mixin,
        ["style"] = StyleDeferredKind.StyleMacro,
        ["shape"] = StyleDeferredKind.ShapeMacro,
        ["effect"] = StyleDeferredKind.EffectMacro,
    };

    readonly IReadOnlyCollection<string> _importChain;
    readonly List<StyleSheet> _imports = [];
    readonly List<StyleRule> _ownRules = [];
    readonly Dictionary<string, int> _inheritanceOffsets = new(StringComparer.Ordinal);
    string _text;
    int _order;

    StyleSheetParser(string text, StyleSheetOptions options, IReadOnlyCollection<string> importChain)
    {
        _text = text;
        Options = options;
        _importChain = importChain;
    }

    public StyleSheetOptions Options { get; }
    public List<StyleRule> Rules { get; } = [];
    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> Constants { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, IReadOnlyList<string>> Inheritance { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, IReadOnlyList<string>> PropertyInheritance { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, IReadOnlyList<string>> SelectorInheritance { get; } = new(StringComparer.Ordinal);
    public List<StyleFontFace> FontFaces { get; } = [];
    public List<StyleDeferredConstruct> Deferred { get; } = [];

    bool Css => Options.AllowCssSyntax;

    Dictionary<string, IReadOnlyList<string>>[] InheritanceMaps =>
        [Inheritance, PropertyInheritance, SelectorInheritance];

    /// <summary>Parses <paramref name="text"/>; <paramref name="importChain"/> holds the ids being imported.</summary>
    public static StyleSheet Parse(string text, StyleSheetOptions options, IReadOnlyCollection<string> importChain)
    {
        var parser = new StyleSheetParser(StyleSheetLexer.BlankComments(text), options, importChain);
        parser.CollectImports();
        parser.CollectConstants();
        parser.ParseTopLevel();
        parser.ValidateInheritance();
        parser.Assemble();
        return new StyleSheet(parser);
    }

    void CollectImports() => _text = BlankMatches(ImportPattern, Import);

    void Import(Match match)
    {
        var id = match.Groups["id"].Value;
        var resolved = ResolveImport(id, match.Index);
        var childOptions = Options with
        {
            SourceName = resolved.SourceName ?? id,
            BaseUri = resolved.BaseUri ?? Options.BaseUri,
        };
        var chain = new List<string>(_importChain) { id };
        if (Options.SourceName is { } self) chain.Add(self);
        var imported = Parse(resolved.Text, childOptions, chain);
        _imports.Add(imported);
        foreach (var (name, value) in imported.Constants) Constants[name] = value;
    }

    StyleSheetText ResolveImport(string id, int offset)
    {
        if (Options.ImportResolver is not { } resolver)
            throw Error(offset, $"@import \"{id}\" needs StyleSheetOptions.ImportResolver");
        if (_importChain.Contains(id, StringComparer.Ordinal) || id == Options.SourceName)
            throw Error(offset, $"Cyclic @import of \"{id}\"");
        return resolver(id) ?? throw Error(offset, $"Cannot resolve @import \"{id}\"");
    }

    void CollectConstants() => _text = BlankMatches(ConstPattern, match =>
        Constants[match.Groups[1].Value] = SubstituteConstants(match.Groups[2].Value.Trim()));

    string BlankMatches(Regex pattern, Action<Match> onMatch)
    {
        char[]? buffer = null;
        foreach (Match match in pattern.Matches(_text))
        {
            onMatch(match);
            buffer ??= _text.ToCharArray();
            StyleSheetLexer.Blank(buffer, match.Index, match.Index + match.Length);
        }
        return buffer is null ? _text : new string(buffer);
    }

    string SubstituteConstants(string value) =>
        Constants.Count == 0 || !value.Contains('@')
            ? value
            : ConstReference.Replace(value, m => Constants.GetValueOrDefault(m.Groups[1].Value) ?? m.Value);

    void ParseTopLevel()
    {
        var i = 0;
        while (SkipTrivia(ref i))
        {
            if (_text[i] == '@') i = ParseAtRule(i, topLevel: true);
            else if (_text[i] == '$' || IsCustomPropertyStart(i)) i = ParseTopLevelVariable(i);
            else i = ParseRule(i);
        }
    }

    bool SkipTrivia(ref int i)
    {
        while (i < _text.Length && (char.IsWhiteSpace(_text[i]) || _text[i] == ';')) i++;
        return i < _text.Length;
    }

    bool IsCustomPropertyStart(int i) => _text[i] == '-' && i + 1 < _text.Length && _text[i + 1] == '-';

    int ParseTopLevelVariable(int i)
    {
        var end = StyleSheetLexer.FindBoundary(_text, i);
        if (end >= 0 && _text[end] != ';') throw Error(i, "Expected ';' after a top-level token");
        if (end < 0) end = _text.Length;
        var (name, value) = SplitDeclaration(_text[i..end].Trim(), i);
        Variables[name] = value;
        return end + 1;
    }

    int ParseRule(int i)
    {
        var brace = StyleSheetLexer.FindBoundary(_text, i);
        if (brace < 0 || _text[brace] != '{') throw Error(i, "Expected a rule block");
        var selectorText = _text[i..brace].Trim();
        if (selectorText.Length == 0) throw Error(i, "Missing selector");
        selectorText = ParseInheritance(selectorText, i);
        return ParseBlock(brace + 1, SplitSelectors(selectorText), i);
    }

    string ParseInheritance(string selectorText, int offset)
    {
        var inherit = InheritPattern.Match(selectorText);
        if (!inherit.Success) return selectorText;
        var child = inherit.Groups["child"].Value;
        var target = inherit.Groups["kind"].Value switch
        {
            "inherit-properties" => PropertyInheritance,
            "inherit-selector" => SelectorInheritance,
            _ => Inheritance,
        };
        target[child] = inherit.Groups["parents"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _inheritanceOffsets[child] = offset;
        return child;
    }

    int ParseBlock(int i, string[] selectorTexts, int selectorOffset)
    {
        var ownOrder = _order++;
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = new List<int> { LineOf(selectorOffset) };
        while (true)
        {
            if (!SkipTrivia(ref i)) throw Error(i, "Unterminated rule block: missing '}'");
            if (_text[i] == '}') { i++; break; }
            i = ParseBlockStatement(i, selectorTexts, declarations, lines);
        }

        if (declarations.Count > 0)
            _ownRules.Add(CreateRule(selectorTexts, declarations, ownOrder, selectorOffset, lines));
        return i;
    }

    int ParseBlockStatement(int i, string[] selectorTexts, Dictionary<string, string> declarations, List<int> lines)
    {
        if (_text[i] == '@') return ParseAtRule(i, topLevel: false);
        var end = StyleSheetLexer.FindBoundary(_text, i);
        if (end < 0) throw Error(i, "Unterminated declaration");
        var head = _text[i..end].Trim();
        if (_text[end] == '{')
        {
            if (StartsWithKeyword(head, "effect")) return Defer(StyleDeferredKind.Effect, i);
            var nested = SplitSelectors(head)
                .SelectMany(child => selectorTexts.Select(parent => Combine(parent, child)))
                .ToArray();
            return ParseBlock(end + 1, nested, i);
        }

        var next = _text[end] == ';' ? end + 1 : end;
        if (StartsWithKeyword(head, "shape")) return Defer(StyleDeferredKind.Shape, i);
        if (IsBackgroundShape(head)) return Defer(StyleDeferredKind.BackgroundShape, i);
        AddDeclaration(head, i, declarations, lines);
        return next;
    }

    /// <summary>Records a <c>name = value</c> declaration and the source line it is on.</summary>
    void AddDeclaration(string head, int offset, Dictionary<string, string> declarations, List<int> lines)
    {
        var (property, value) = SplitDeclaration(head, offset);
        declarations[property] = value;
        var line = LineOf(offset);
        if (lines[^1] != line) lines.Add(line);
    }

    List<int>? _lineStarts;

    /// <summary>The 1-based line of <paramref name="offset"/>, by binary search over the line starts.</summary>
    int LineOf(int offset)
    {
        if (_lineStarts is null)
        {
            _lineStarts = [0];
            for (var j = 0; j < _text.Length; j++)
                if (_text[j] == '\n') _lineStarts.Add(j + 1);
        }
        var index = _lineStarts.BinarySearch(offset);
        return index >= 0 ? index + 1 : ~index;
    }

    (string Name, string Value) SplitDeclaration(string declaration, int offset)
    {
        var separator = DeclarationSeparator(declaration);
        if (separator <= 0) throw Error(offset, $"Malformed declaration '{declaration}'{CssHint(declaration)}");
        var name = declaration[..separator].Trim();
        var value = SubstituteConstants(declaration[(separator + 1)..].Trim());
        ValidateValue(declaration, separator, value, offset);
        if (name[0] == '$') return ($"--{name[1..]}", value);
        if (!Css) RejectCssForms(name, value, offset);
        return (name, value);
    }

    /// <summary>
    /// Rejects malformed expressions at parse time. The column points into the value when no <c>@const</c> changed
    /// its text, otherwise at the value's start.
    /// </summary>
    void ValidateValue(string declaration, int separator, string value, int offset)
    {
        try
        {
            StyleExpression.Validate(value);
        }
        catch (StyleExpressionException exception)
        {
            var raw = declaration[(separator + 1)..];
            var start = offset + separator + 1 + (raw.Length - raw.TrimStart().Length);
            var position = raw.Trim() == value ? start + exception.Position : start;
            throw Error(position, $"Invalid value '{value}': {exception.Message}");
        }
    }

    string CssHint(string declaration) =>
        !Css && declaration.Contains(':') ? " (the CSS ':' form needs StyleSheetOptions.AllowCssSyntax)" : string.Empty;

    void RejectCssForms(string name, string value, int offset)
    {
        if (name.StartsWith("--", StringComparison.Ordinal))
            throw Error(offset, $"CSS custom property '{name}' needs StyleSheetOptions.AllowCssSyntax; use ${name[2..]}");
        if (value.Contains("var(--", StringComparison.Ordinal))
            throw Error(offset, "var() needs StyleSheetOptions.AllowCssSyntax; use $name");
    }

    int DeclarationSeparator(string text)
    {
        var equals = text.IndexOf('=');
        if (!Css) return equals;
        var colon = text.IndexOf(':');
        if (equals < 0) return colon;
        return colon < 0 ? equals : Math.Min(equals, colon);
    }

    int ParseAtRule(int i, bool topLevel)
    {
        var nameEnd = i + 1;
        while (nameEnd < _text.Length && StyleSheetLexer.IsIdentifierChar(_text[nameEnd])) nameEnd++;
        var name = _text[(i + 1)..nameEnd];
        if (name.Length == 0) throw Error(i, "Expected a name after '@'");
        if (topLevel && name == "font-face") return ParseFontFace(i, nameEnd);
        if (MalformedAtRules.TryGetValue(name, out var message)) throw Error(i, message);
        return Defer(DeferredAtRules.GetValueOrDefault(name, StyleDeferredKind.Invocation), i);
    }

    int Defer(StyleDeferredKind kind, int i)
    {
        var end = StyleSheetLexer.FindBoundary(_text, i);
        if (end < 0) throw Error(i, "Unterminated statement");
        var after = _text[end] switch
        {
            '{' => StyleSheetLexer.SkipBlock(_text, end),
            ';' => end + 1,
            _ => end,
        };
        if (after < 0) throw Error(i, "Unterminated block: missing '}'");
        var (line, column) = StyleSheetLexer.Position(_text, i);
        Deferred.Add(new StyleDeferredConstruct(kind, _text[i..after].Trim(), line, column));
        return after;
    }

    int ParseFontFace(int i, int nameEnd)
    {
        var open = StyleSheetLexer.FindBoundary(_text, nameEnd);
        if (open < 0 || _text[open] != '{' || !string.IsNullOrWhiteSpace(_text[nameEnd..open]))
            throw Error(i, "Expected '{' after @font-face");
        var (declarations, close) = ParseFontFaceDeclarations(open + 1, i);
        if (!declarations.TryGetValue("font-family", out var family)
            || !declarations.TryGetValue("src", out var src)
            || !StyleValue.TryUrl(src, Options.BaseUri, out var uri))
            throw Error(i, "@font-face needs font-family and a valid src");
        FontFaces.Add(new StyleFontFace(StyleValue.Unquote(family), uri)
        {
            Weight = StyleFonts.Weight(declarations.GetValueOrDefault("font-weight")),
            Italic = StyleFonts.Italic(declarations.GetValueOrDefault("font-style")),
        });
        return close + 1;
    }

    /// <summary>Reads <c>name = value;</c> pairs up to the closing <c>}</c>, returning them and its index.</summary>
    (Dictionary<string, string> Declarations, int Close) ParseFontFaceDeclarations(int j, int ruleOffset)
    {
        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            if (!SkipTrivia(ref j)) throw Error(ruleOffset, "Unterminated @font-face block: missing '}'");
            if (_text[j] == '}') return (declarations, j);
            var end = StyleSheetLexer.FindBoundary(_text, j);
            if (end < 0 || _text[end] == '{') throw Error(j, "Malformed @font-face declaration");
            var (name, value) = SplitDeclaration(_text[j..end].Trim(), j);
            declarations[name] = value;
            j = _text[end] == ';' ? end + 1 : end;
        }
    }

    StyleRule CreateRule(string[] selectorTexts, Dictionary<string, string> declarations, int order, int offset,
        List<int> lines)
    {
        var selectors = new Selector[selectorTexts.Length];
        for (var s = 0; s < selectors.Length; s++)
        {
            try
            {
                selectors[s] = Selector.Parse(selectorTexts[s]);
            }
            catch (FormatException exception)
            {
                throw Error(offset, exception.Message);
            }
        }
        return new StyleRule
        {
            Selectors = selectors,
            Declarations = declarations,
            Order = order,
            BaseUri = Options.BaseUri,
            Lines = lines,
        };
    }

    /// <summary>Whether a declaration assigns <c>bg-shape</c>, whose value is shape algebra rather than a scalar.</summary>
    bool IsBackgroundShape(string head)
    {
        var separator = DeclarationSeparator(head);
        return separator > 0 && head.AsSpan(0, separator).Trim().SequenceEqual("bg-shape");
    }

    static bool StartsWithKeyword(string head, string keyword) =>
        head.Length > keyword.Length && head.StartsWith(keyword, StringComparison.Ordinal)
                                     && char.IsWhiteSpace(head[keyword.Length]);

    static string[] SplitSelectors(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    static string Combine(string parent, string child)
    {
        if (child.Contains('&', StringComparison.Ordinal)) return child.Replace("&", parent, StringComparison.Ordinal);
        return child.StartsWith(':') ? $"{parent}{child}" : $"{parent} {child}";
    }

    void ValidateInheritance()
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in _inheritanceOffsets.Keys) Visit(child, child);
        return;

        void Visit(string child, string root)
        {
            if (visited.Contains(child)) return;
            if (!visiting.Add(child))
                throw Error(_inheritanceOffsets.GetValueOrDefault(child, _inheritanceOffsets[root]),
                    $"Cyclic style inheritance involving '{child}'");
            foreach (var map in InheritanceMaps)
                if (map.TryGetValue(child, out var parents))
                    foreach (var parent in parents) Visit(parent, root);
            visiting.Remove(child);
            visited.Add(child);
        }
    }

    void Assemble()
    {
        var offset = 0;
        foreach (var imported in _imports) offset = AppendRules(imported.Rules, offset);
        foreach (var rule in _ownRules) Rules.Add(offset == 0 ? rule : Renumber(rule, offset + rule.Order));
        if (_imports.Count > 0) LayerImports();
    }

    /// <summary>Appends rules renumbered after <paramref name="offset"/>; returns the offset for the next sheet.</summary>
    int AppendRules(IReadOnlyList<StyleRule> rules, int offset)
    {
        var count = 0;
        foreach (var rule in rules)
        {
            Rules.Add(Renumber(rule, offset + rule.Order));
            count = Math.Max(count, rule.Order + 1);
        }
        return offset + count;
    }

    void LayerImports()
    {
        Layer(Variables, sheet => sheet.Variables);
        Layer(Inheritance, sheet => sheet.Inheritance);
        Layer(PropertyInheritance, sheet => sheet.PropertyInheritance);
        Layer(SelectorInheritance, sheet => sheet.SelectorInheritance);
        FontFaces.InsertRange(0, _imports.SelectMany(sheet => sheet.FontFaces));
        Deferred.InsertRange(0, _imports.SelectMany(sheet => sheet.Deferred));
    }

    void Layer<T>(Dictionary<string, T> own, Func<StyleSheet, IReadOnlyDictionary<string, T>> select)
    {
        var layered = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var imported in _imports)
            foreach (var (key, value) in select(imported)) layered[key] = value;
        foreach (var (key, value) in own) layered[key] = value;
        own.Clear();
        foreach (var (key, value) in layered) own[key] = value;
    }

    static StyleRule Renumber(StyleRule rule, int order) => new()
    {
        Selectors = rule.Selectors,
        Declarations = rule.Declarations,
        Order = order,
        BaseUri = rule.BaseUri,
        Lines = rule.Lines,
    };

    StyleSheetException Error(int offset, string message)
    {
        var (line, column) = StyleSheetLexer.Position(_text, offset);
        return new StyleSheetException(Options.SourceName, line, column, message);
    }
}

namespace Guinevere;

/// <summary>A selector supporting compounds, descendant and direct-child combinators.</summary>
public sealed class Selector
{
    enum Combinator { None, Descendant, Child }
    sealed record Part(string? Type, string? Id, string[] Classes, StyleState State,
        string[] Modifiers, Combinator Relation);
    readonly Part[] _parts;

    /// <summary>The built-in state required by the right-most compound selector.</summary>
    public StyleState Modifier => _parts[^1].State;
    /// <summary>CSS-like cascade weight: ids = 100, classes/modifiers = 10, types = 1.</summary>
    public int Specificity { get; }

    internal bool HasCombinator => _parts.Length > 1;

    Selector(Part[] parts)
    {
        _parts = parts;
        Specificity = parts.Sum(p => (p.Id is null ? 0 : 100)
                                     + (p.Classes.Length + p.Modifiers.Length + PopCount(p.State)) * 10
                                     + (p.Type is null or "*" ? 0 : 1));
    }

    /// <summary>
    /// Parses a selector such as <c>Panel &gt; Button.primary:checked:hover</c>; CSS escapes such as
    /// <c>icon#scene\.move</c> put a literal <c>.</c>, <c>#</c> or <c>:</c> in a name.
    /// </summary>
    public static Selector Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var tokens = Tokenize(text);
        var parts = new List<Part>();
        var relation = Combinator.None;
        foreach (var token in tokens)
        {
            if (token == ">")
            {
                if (parts.Count == 0 || relation == Combinator.Child)
                    throw new FormatException($"Malformed child combinator in '{text}'");
                relation = Combinator.Child;
                continue;
            }
            if (parts.Count > 0 && relation == Combinator.None) relation = Combinator.Descendant;
            parts.Add(ParsePart(token, relation, text));
            relation = Combinator.None;
        }
        if (parts.Count == 0 || relation != Combinator.None) throw new FormatException($"Malformed selector '{text}'");
        return new Selector([.. parts]);
    }

    /// <summary>Whether this selector matches the target and its nearest-first ancestor list.</summary>
    public bool Matches(in StyleTarget target)
    {
        if (!MatchesPart(_parts[^1], target)) return false;
        var ancestors = target.Ancestors ?? [];
        var ancestorIndex = 0;
        for (var partIndex = _parts.Length - 2; partIndex >= 0; partIndex--)
        {
            var relation = _parts[partIndex + 1].Relation;
            if (relation == Combinator.Child)
            {
                if (ancestorIndex >= ancestors.Count || !MatchesPart(_parts[partIndex], ancestors[ancestorIndex]))
                    return false;
                ancestorIndex++;
                continue;
            }
            while (ancestorIndex < ancestors.Count && !MatchesPart(_parts[partIndex], ancestors[ancestorIndex]))
                ancestorIndex++;
            if (ancestorIndex >= ancestors.Count) return false;
            ancestorIndex++;
        }
        return true;
    }

    static Part ParsePart(string text, Combinator relation, string whole)
    {
        var annotation = text.IndexOf('(');
        if (annotation >= 0) text = text[..annotation];
        var part = new PartBuilder(whole);
        var i = 0;
        while (i < text.Length)
        {
            var prefix = text[i];
            if (prefix is '.' or '#' or ':') i++;
            part.Add(prefix, ReadName(text, ref i, whole));
        }
        return part.Build(relation);
    }

    /// <summary>Collects one compound selector's names by their prefix.</summary>
    sealed class PartBuilder(string whole)
    {
        readonly List<string> _classes = [];
        readonly List<string> _modifiers = [];
        string? _type;
        string? _id;
        StyleState _state;

        public void Add(char prefix, string value)
        {
            switch (prefix)
            {
                case '.': _classes.Add(value); break;
                case '#':
                    _id = _id is null ? value : throw new FormatException($"Multiple ids in selector '{whole}'");
                    break;
                case ':': AddState(value); break;
                default:
                    _type = _type is null ? value : throw new FormatException($"Malformed selector '{whole}'");
                    break;
            }
        }

        public Part Build(Combinator relation) =>
            new(_type, _id, [.. _classes], _state, [.. _modifiers], relation);

        void AddState(string value)
        {
            var builtIn = BuiltInState(value);
            if (builtIn == StyleState.None) _modifiers.Add(value);
            else _state |= builtIn;
        }
    }

    /// <summary>Reads a name up to the next unescaped <c>.</c>, <c>#</c> or <c>:</c>, resolving its escapes.</summary>
    static string ReadName(string text, ref int i, string whole)
    {
        var start = i;
        while (i < text.Length && text[i] is not ('.' or '#' or ':')) i += text[i] == '\\' ? 2 : 1;
        var value = StyleValue.Unescape(text[start..Math.Min(i, text.Length)]);
        return value.Length > 0 ? value : throw new FormatException($"Malformed selector '{whole}'");
    }

    static StyleState BuiltInState(string name) => name.ToLowerInvariant() switch
    {
        "hover" => StyleState.Hover,
        "active" or "hold" or "pressed" => StyleState.Active,
        "focus" => StyleState.Focus,
        "disabled" => StyleState.Disabled,
        _ => StyleState.None,
    };

    static bool MatchesPart(Part part, in StyleTarget target)
    {
        if ((target.State & part.State) != part.State) return false;
        if (part.Type is not (null or "*") && !string.Equals(part.Type, target.Type, StringComparison.Ordinal)) return false;
        if (part.Id is not null && !string.Equals(part.Id, target.Id, StringComparison.Ordinal)) return false;
        foreach (var cls in part.Classes) if (!target.Classes.Contains(cls, StringComparer.Ordinal)) return false;
        foreach (var modifier in part.Modifiers)
            if (target.Modifiers is null || !target.Modifiers.Contains(modifier, StringComparer.Ordinal)) return false;
        return true;
    }

    static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var start = -1;
        var parentheses = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') parentheses++;
            else if (text[i] == ')') parentheses--;
            if (parentheses < 0) throw new FormatException($"Unbalanced transition annotation in '{text}'");
            if ((parentheses > 0 || !char.IsWhiteSpace(text[i])) && (parentheses > 0 || text[i] != '>'))
            {
                if (start < 0) start = i;
                continue;
            }
            if (start >= 0) { tokens.Add(text[start..i]); start = -1; }
            if (text[i] == '>') tokens.Add(">");
        }
        if (parentheses != 0) throw new FormatException($"Unbalanced transition annotation in '{text}'");
        if (start >= 0) tokens.Add(text[start..]);
        return tokens;
    }

    static int PopCount(StyleState state)
    {
        var value = (int)state;
        var count = 0;
        while (value != 0) { count += value & 1; value >>= 1; }
        return count;
    }
}

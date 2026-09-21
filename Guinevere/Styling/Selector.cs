namespace Guinevere;

/// <summary>Built-in interaction states available to style selectors.</summary>
[Flags]
public enum StyleState
{
    /// <summary>No interaction state.</summary>
    None = 0,
    /// <summary>The pointer is over the element.</summary>
    Hover = 1,
    /// <summary>The element is pressed.</summary>
    Active = 2,
    /// <summary>The element has keyboard focus.</summary>
    Focus = 4,
    /// <summary>The element is disabled.</summary>
    Disabled = 8,
}

/// <summary>What a selector is matched against in the current frame.</summary>
public readonly record struct StyleTarget(string? Type, string? Id, IReadOnlyList<string> Classes,
    StyleState State = StyleState.None, IReadOnlyList<string>? Modifiers = null,
    IReadOnlyList<StyleTarget>? Ancestors = null);

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

    Selector(Part[] parts)
    {
        _parts = parts;
        Specificity = parts.Sum(p => (p.Id is null ? 0 : 100)
                                     + (p.Classes.Length + p.Modifiers.Length + PopCount(p.State)) * 10
                                     + (p.Type is null or "*" ? 0 : 1));
    }

    /// <summary>Parses a selector such as <c>Panel &gt; Button.primary:checked:hover</c>.</summary>
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
        string? type = null;
        string? id = null;
        var classes = new List<string>();
        var modifiers = new List<string>();
        var state = StyleState.None;
        var i = 0;
        while (i < text.Length)
        {
            var prefix = text[i];
            if (prefix is '.' or '#' or ':') i++;
            var start = i;
            while (i < text.Length && text[i] is not ('.' or '#' or ':')) i++;
            var value = text[start..i];
            if (value.Length == 0) throw new FormatException($"Malformed selector '{whole}'");
            switch (prefix)
            {
                case '.': classes.Add(value); break;
                case '#':
                    if (id is not null) throw new FormatException($"Multiple ids in selector '{whole}'");
                    id = value;
                    break;
                case ':':
                    var builtIn = value.ToLowerInvariant() switch
                    {
                        "hover" => StyleState.Hover, "active" or "hold" or "pressed" => StyleState.Active,
                        "focus" => StyleState.Focus, "disabled" => StyleState.Disabled, _ => StyleState.None,
                    };
                    if (builtIn == StyleState.None) modifiers.Add(value); else state |= builtIn;
                    break;
                default:
                    if (type is not null) throw new FormatException($"Malformed selector '{whole}'");
                    type = value;
                    break;
            }
        }
        return new Part(type, id, [.. classes], state, [.. modifiers], relation);
    }

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
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]) && text[i] != '>') { if (start < 0) start = i; continue; }
            if (start >= 0) { tokens.Add(text[start..i]); start = -1; }
            if (text[i] == '>') tokens.Add(">");
        }
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

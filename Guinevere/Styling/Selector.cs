namespace Guinevere;

/// <summary>Interaction states a <c>.uss</c> modifier (<c>:hover</c>, <c>:active</c>, …) can target.</summary>
[Flags]
public enum StyleState
{
    /// <summary>No interaction.</summary>
    None = 0,

    /// <summary>Pointer is over the element.</summary>
    Hover = 1,

    /// <summary>Element is pressed / held.</summary>
    Active = 2,

    /// <summary>Element has keyboard focus.</summary>
    Focus = 4,

    /// <summary>Element is disabled.</summary>
    Disabled = 8,
}

/// <summary>What a <see cref="Selector"/> is matched against — one styled element in one frame.</summary>
/// <param name="Type">Element type name (e.g. <c>Button</c>), or <c>null</c>.</param>
/// <param name="Id">Element id / <c>name</c>, or <c>null</c>.</param>
/// <param name="Classes">The element's classes.</param>
/// <param name="State">The element's current interaction state.</param>
public readonly record struct StyleTarget(
    string? Type,
    string? Id,
    IReadOnlyList<string> Classes,
    StyleState State = StyleState.None);

/// <summary>
/// A single compound <c>.uss</c> selector: an optional type, an optional id, zero or more classes,
/// and an optional state modifier — e.g. <c>Button.primary:hover</c>, <c>#save</c>, <c>.row</c>, <c>*</c>.
/// Descendant / child combinators are not supported yet.
/// </summary>
public sealed class Selector
{
    readonly string? _type;   // null or "*" means "any type"
    readonly string? _id;
    readonly string[] _classes;

    /// <summary>The state modifier this selector requires, or <see cref="StyleState.None"/>.</summary>
    public StyleState Modifier { get; }

    /// <summary>
    /// Cascade weight: id = 100, class / state = 10, type = 1. Higher wins; equal weights fall back
    /// to source order.
    /// </summary>
    public int Specificity { get; }

    Selector(string? type, string? id, string[] classes, StyleState modifier)
    {
        _type = type;
        _id = id;
        _classes = classes;
        Modifier = modifier;
        Specificity = (id is null ? 0 : 100)
                      + (classes.Length + (modifier == StyleState.None ? 0 : 1)) * 10
                      + (type is null or "*" ? 0 : 1);
    }

    /// <summary>Parses one compound selector. Whitespace is not allowed (no combinators).</summary>
    /// <param name="text">The selector text, e.g. <c>Button.primary:hover</c>.</param>
    /// <exception cref="FormatException">The selector contains an unsupported combinator or token.</exception>
    public static Selector Parse(string text)
    {
        var s = text.Trim();
        if (s.Length == 0) throw new FormatException("Empty selector");
        if (s.Contains(' ') || s.Contains('>'))
            throw new FormatException($"Descendant/child combinators are not supported yet: '{text}'");

        var modifier = StyleState.None;
        var colon = s.IndexOf(':');
        if (colon >= 0)
        {
            modifier = s[(colon + 1)..].Trim().ToLowerInvariant() switch
            {
                "hover" => StyleState.Hover,
                "active" or "hold" or "pressed" => StyleState.Active,
                "focus" => StyleState.Focus,
                "disabled" => StyleState.Disabled,
                var other => throw new FormatException($"Unknown pseudo-class ':{other}'"),
            };
            s = s[..colon];
        }

        string? id = null;
        var classes = new List<string>();
        string? type = null;

        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c is '#' or '.')
            {
                var start = ++i;
                while (i < s.Length && s[i] is not ('#' or '.')) i++;
                var token = s[start..i];
                if (token.Length == 0) throw new FormatException($"Malformed selector '{text}'");
                if (c == '#') id = token;
                else classes.Add(token);
            }
            else
            {
                var start = i;
                while (i < s.Length && s[i] is not ('#' or '.')) i++;
                type = s[start..i];
            }
        }

        return new Selector(type, id, [.. classes], modifier);
    }

    /// <summary>Whether this selector matches <paramref name="target"/> in the current frame.</summary>
    /// <param name="target">The styled element.</param>
    public bool Matches(in StyleTarget target)
    {
        if (Modifier != StyleState.None && (target.State & Modifier) == 0)
            return false;

        if (_type is not (null or "*") && !string.Equals(_type, target.Type, StringComparison.Ordinal))
            return false;

        if (_id is not null && !string.Equals(_id, target.Id, StringComparison.Ordinal))
            return false;

        foreach (var cls in _classes)
            if (!target.Classes.Contains(cls))
                return false;

        return true;
    }
}

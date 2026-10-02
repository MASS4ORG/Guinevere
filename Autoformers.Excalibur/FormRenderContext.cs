namespace Autoformers;

/// <summary>
/// What a host supplies to draw forms: drawers, fold state and its own rules. Keep one per panel so fold state
/// persists across frames.
/// </summary>
public sealed class FormRenderContext
{
    /// <summary>The drawer scope fields are dispatched through.</summary>
    public FormDrawers Drawers { get; init; } = FormDrawers.Default;

    /// <summary>Ids of the collection and nested-object headings the user has folded shut.</summary>
    public ISet<string> Collapsed { get; init; } = new HashSet<string>();

    /// <summary>The page each paged collection shows, by collection id; clamped to the pages that exist.</summary>
    public IDictionary<string, int> Pages { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Whether a value of the given type may be expanded in place as a nested form, on top of the built-in rule
    /// (not primitive, enum, string, decimal or a System type, and has editable members). Null allows all.
    /// </summary>
    public Func<Type, bool>? CanInline { get; init; }

    /// <summary>Whether a field differs from its source, drawn as a bold label and an accent margin bar.</summary>
    public Func<FormField, bool>? IsModified { get; init; }

    /// <summary>Translates enum labels; null shows them as declared.</summary>
    public Func<string, string>? Translate { get; init; }

    internal bool Modified(FormField field) => IsModified?.Invoke(field) == true;
}

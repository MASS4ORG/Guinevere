namespace Guinevere;

/// <summary>An open completion list: the provider's items narrowed to what has been typed since it opened.</summary>
public sealed class CompletionSession
{
    readonly IReadOnlyList<CompletionItem> _all;

    internal CompletionSession(CompletionList list)
    {
        ReplaceStart = list.ReplaceStart;
        _all = list.Items;
        Items = list.Items;
    }

    /// <summary>Start of the range an accepted item replaces.</summary>
    public int ReplaceStart { get; }

    /// <summary>The items matching the typed prefix, best first.</summary>
    public IReadOnlyList<CompletionItem> Items { get; private set; }

    /// <summary>Index of the highlighted item.</summary>
    public int Selected { get; internal set; }

    /// <summary>The highlighted item, or null when nothing matches.</summary>
    public CompletionItem? SelectedItem => Items.Count == 0 ? null : Items[Math.Clamp(Selected, 0, Items.Count - 1)];

    /// <summary>Narrows the items to those starting with the prefix, ignoring case; returns whether any remain.</summary>
    internal bool Filter(string prefix)
    {
        var selected = SelectedItem;
        Items = prefix.Length == 0
            ? _all
            : [.. _all.Where(item => item.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Label, prefix, StringComparison.Ordinal))];
        Selected = selected is null ? 0 : Math.Max(0, Items.ToList().IndexOf(selected));
        return Items.Count > 0;
    }

    /// <summary>Moves the highlight, wrapping around the list.</summary>
    internal void MoveSelection(int delta)
    {
        if (Items.Count > 0)
        {
            Selected = ((Selected + delta) % Items.Count + Items.Count) % Items.Count;
        }
    }
}

namespace Guinevere;

/// <summary>One tab as added by <see cref="TabBuilder"/>, rebuilt every frame.</summary>
class TabInfo
{
    /// <summary>What the tab shows.</summary>
    public Content Title { get; set; }

    /// <summary>Stable key for closing and restoring; the explicit key, the title's label, or the position.</summary>
    public string? Key { get; set; }

    /// <summary>Whether the tab can be picked.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the user may close this tab, with the middle-click or the "×" button. Defaults to false.
    /// </summary>
    public bool Closable { get; set; }

    /// <summary>Builds the page shown while this tab is active.</summary>
    public Action? Content { get; set; }

    /// <summary>Extra classes for this tab's <c>tab</c> node.</summary>
    public IReadOnlyList<string>? Classes { get; set; }
}

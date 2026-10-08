namespace Guinevere;

class TabInfo
{
    public string Title { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the user may close this tab, with the middle-click or the "×" button. Defaults to false.
    /// </summary>
    public bool Closable { get; set; }

    public Action? Content { get; set; }

    /// <summary>Extra classes for this tab's <c>tab</c> node.</summary>
    public IReadOnlyList<string>? Classes { get; set; }
}

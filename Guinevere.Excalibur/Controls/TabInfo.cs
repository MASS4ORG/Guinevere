namespace Guinevere;

internal class TabInfo
{
    public string Title { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the user may close this tab, with the middle-click or the "×" button. Defaults to false.
    /// </summary>
    public bool Closable { get; set; }

    public Action? Content { get; set; }
    public Color? BackgroundColor { get; set; }
    public Color? TextColor { get; set; }
}

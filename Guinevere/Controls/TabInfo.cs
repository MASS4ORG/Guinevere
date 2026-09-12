namespace Guinevere;

internal class TabInfo
{
    public string Title { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether the user may close this tab, with the middle-click or the "×" button. Defaults to true.
    /// </summary>
    public bool Closable { get; set; } = true;
    public Action? Content { get; set; }
    public Color? BackgroundColor { get; set; }
    public Color? TextColor { get; set; }
}

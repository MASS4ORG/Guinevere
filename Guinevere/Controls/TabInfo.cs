namespace Guinevere;

internal class TabInfo
{
    public string Title { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Action? Content { get; set; }
    public Color? BackgroundColor { get; set; }
    public Color? TextColor { get; set; }
}

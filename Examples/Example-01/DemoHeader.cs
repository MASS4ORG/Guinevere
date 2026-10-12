using Guinevere;

namespace GuinevereDemos;

/// <summary>Draws the shared demo header with the badge embedded in the example assembly.</summary>
static class DemoHeader
{
    static readonly Bitmap Badge = LoadBadge();

    /// <summary>Draws the cached badge, demo title and frame rate.</summary>
    public static void Header(Gui gui, string title, Color? textColor = null)
    {
        var color = textColor ?? Color.White;
        using (gui.Node().Height(40).Padding(15, 0).Direction(Axis.Horizontal)
                   .ContentAlignY(0.5f).Gap(8).Enter())
        {
            gui.Image(Badge, width: 26, height: 26);
            gui.DrawText(title, 16, color);
            gui.Node().Expand();
            gui.DrawText($"FPS: {gui.Time.SmoothFps:N1}", 12, color);
        }
    }

    static Bitmap LoadBadge()
    {
        using var stream = typeof(DemoHeader).Assembly.GetManifestResourceStream("GuinevereDemos.guinevere-badge.png")
                           ?? throw new InvalidOperationException("The embedded demo badge is missing.");
        return Bitmap.FromStream(stream);
    }
}

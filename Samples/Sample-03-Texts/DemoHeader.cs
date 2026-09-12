using Guinevere;

namespace GuinevereDemos;

internal static class DemoHeader
{
    private static readonly Bitmap Badge = LoadBadge();

    public static void Header(Gui gui, string title)
    {
        using (gui.Node().Height(40).Padding(15, 0).Direction(Axis.Horizontal)
                   .ContentAlignY(0.5f).Gap(8).Enter())
        {
            gui.Image(Badge, width: 26, height: 26);
            gui.DrawText(title, 16, Color.White);
            gui.Node().Expand();
            gui.DrawText($"FPS: {gui.Time.SmoothFps:N1}", 12, Color.White);
        }
    }

    private static Bitmap LoadBadge()
    {
        // The badge sits at the repository root; walk up from the output directory to find it.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "guinevere-badge.png");
            if (File.Exists(file)) return Bitmap.FromFile(file);
        }

        return Bitmap.FromFile("guinevere-badge.png");
    }
}

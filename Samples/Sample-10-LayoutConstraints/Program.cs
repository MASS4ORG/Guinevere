using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_10_LayoutConstraints;

/// <summary>
/// Shows <c>WidthPercent</c> / <c>HeightPercent</c> and the <c>MinWidth</c> / <c>MaxWidth</c> /
/// <c>MinHeight</c> / <c>MaxHeight</c> constraints.
/// </summary>
public abstract class Program
{
    private static readonly Color Page = Color.FromArgb(255, 24, 26, 34);
    private static readonly Color Panel = Color.FromArgb(255, 46, 50, 62);
    private static readonly Color Bar = Color.FromArgb(255, 90, 170, 255);
    private static readonly Color Ink = Color.FromArgb(255, 235, 238, 245);

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 900, 640, "Layout Constraints");
        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Page);
        gui.DrawWindowTitlebar();

        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(32).Gap(20).Enter())
        {
            gui.DrawText("WidthPercent — 25% / 50% / 75% / 100% of the container", 15, Ink);
            using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(8).Padding(12).Enter())
            {
                gui.DrawBackgroundRect(Panel, radius: 8);
                foreach (var f in new[] { 0.25f, 0.5f, 0.75f, 1.0f })
                    using (gui.Node(height: 24).WidthPercent(f).Enter())
                        gui.DrawBackgroundRect(Bar, radius: 6);
            }

            gui.DrawText("MaxWidth(420) — an Expand child that stops growing", 15, Ink);
            using (gui.Node(height: 36).ExpandWidth().MaxWidth(420f).Enter())
                gui.DrawBackgroundRect(Bar, radius: 6);

            gui.DrawText("MinWidth(150) — chips forced to a minimum width", 15, Ink);
            using (gui.Node(height: 44).ExpandWidth().Direction(Axis.Horizontal).Gap(12).Enter())
            {
                foreach (var label in new[] { "A", "BB", "CCC" })
                    using (gui.Node(height: 44).MinWidth(150f).Enter())
                    {
                        gui.DrawBackgroundRect(Panel, radius: 8);
                        gui.DrawText(label, 15, Ink);
                    }
            }

            gui.DrawText("HeightPercent(0.6) — fills 60% of the remaining column", 15, Ink);
            using (gui.Node().ExpandWidth().Direction(Axis.Vertical).ExpandHeight().Enter())
            {
                using (gui.Node().ExpandWidth().HeightPercent(0.6f).Enter())
                    gui.DrawBackgroundRect(Bar, radius: 8);
            }
        }
    }
}

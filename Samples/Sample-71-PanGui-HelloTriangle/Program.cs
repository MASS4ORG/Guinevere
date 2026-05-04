using System.Numerics;
using Guinevere;
using Guinevere.Vulkan.SilkNET;
using static System.MathF;

namespace Sample_71_PanGui_HelloTriangle;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 800, 800, "Hello Triangle!");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 41, 41, 41));
        gui.DrawWindowTitlebar();

        using (gui.Node().Expand().Gap(40).Margin(40).AlignContent(0.5f).Enter())
        {
            gui.DrawBackgroundRect(Color.Black, radius: 20);

            var time = gui.Time.Elapsed * 2;

            var center = gui.Node(200, 200).Rect.Center;
            var p1 = center + new Vector2(-100 * Cos(time), -100 + Sin(time) * 10);
            var p2 = center + new Vector2(+100 * Cos(time), -100 - Sin(time) * 10);
            var p3 = center + new Vector2(0, 100);
            gui.DrawTriangle(p1, p2, p3, Color.Red, Color.Green, Color.Blue);

            gui.DrawText("Hello, Triangle!", color: Color.White, size: 50);
        }
    }
}

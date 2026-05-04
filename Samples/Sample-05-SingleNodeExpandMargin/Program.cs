using Guinevere;
using Guinevere.OpenGL.Raylib;

namespace Sample_05_SingleNodeExpandMargin;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 800, 600, "Single Node Expand & Margin Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.Black);

        using (gui.Node().Expand().Gap(10).Margin(40).AlignContent(0.5f).Enter())
        {
            gui.DrawBackgroundRect(Color.White);

            using (gui.Node().Expand().Margin(10).Enter())
            {
                gui.DrawRect(gui.CurrentNode.OuterRect, Color.LightGray);
                gui.DrawRect(gui.CurrentNode.Rect, Color.Red);
            }

            using (gui.Node().Expand().Margin(10).Enter())
            {
                gui.DrawRect(gui.CurrentNode.OuterRect, Color.LightGray);
                gui.DrawRect(gui.CurrentNode.Rect, Color.Orange);
            }

            using (gui.Node().Expand().Margin(10).Enter())
            {
                gui.DrawRect(gui.CurrentNode.OuterRect, Color.LightGray);
                gui.DrawRect(gui.CurrentNode.Rect, Color.Yellow);
            }

            for (var i = 0; i < 3; i++)
            {
                using (gui.Node().Expand().Margin(10).Enter())
                {
                    gui.DrawRect(gui.CurrentNode.OuterRect, Color.LightGray);
                    gui.DrawRect(gui.CurrentNode.Rect, Color.Green, radius: 10 * i);
                }
            }
        }
    }
}

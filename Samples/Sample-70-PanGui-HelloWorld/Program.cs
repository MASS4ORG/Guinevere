using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_70_PanGui_HelloWorld;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 800, 600, "Hello World!");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 29, 29, 29));
        gui.DrawText("Hello, world!", size: 32, color: Color.White);
    }
}

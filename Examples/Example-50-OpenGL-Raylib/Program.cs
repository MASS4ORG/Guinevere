using Guinevere;

namespace Example_50_OpenGL_Raylib;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        Controls_01.Program.ApplyDefaultSheet(gui);
        using var win = new GuiWindow(gui, 1360, 900, "Guinevere Excalibur — Controls & Docking");
        win.RunGui(() => Controls_01.Program.Draw(gui));
    }
}
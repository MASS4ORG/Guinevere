using Guinevere;
using Guinevere.OpenGL.Raylib;
using Example_01;

namespace Example_01_OpenGL_Raylib;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);
        Shared shared = new(gui);
        win.RunGui(shared.Draw);
    }
}

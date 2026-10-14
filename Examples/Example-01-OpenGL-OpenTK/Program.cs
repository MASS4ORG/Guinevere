using Example01;
using Guinevere;

namespace Example01OpenGLOpenTK;

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

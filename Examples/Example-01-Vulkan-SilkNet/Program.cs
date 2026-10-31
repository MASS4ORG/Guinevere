using Example01;
using Guinevere;

namespace Example01VulkanSilkNet;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);
        Example01.Example01 shared = new(gui);
        win.RunGui(shared.Draw);
    }
}

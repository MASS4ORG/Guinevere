using Guinevere;

namespace Example50VulkanSilkNet;

public abstract class Program
{
    public static void Main()
    {
        Environment.SetEnvironmentVariable("SILKNET_USE_WAYLAND", "0");
        var gui = new Gui();
        Controls01.Program.ApplyDefaultSheet(gui);
        using var win = new GuiWindow(gui, 1360, 900, "Guinevere Excalibur — Controls & Docking");
        win.RunGui(() => Controls01.Program.Draw(gui));
    }
}

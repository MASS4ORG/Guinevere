using Guinevere;

namespace Example50OpenGLRaylib;

/// <summary>Entry point for the Example 50 OpenGL Raylib example.</summary>
public abstract class Program
{
    /// <summary>Starts the example application.</summary>
    public static void Main()
    {
        var gui = new Gui();
        Controls01.Program.ApplyDefaultSheet(gui);
        using var win = new GuiWindow(gui, 1360, 900, "Guinevere Excalibur — Controls & Docking");
        win.RunGui(() => Controls01.Program.Draw(gui));
    }
}

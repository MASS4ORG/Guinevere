using Guinevere;

namespace Example01OpenGLRaylib;

/// <summary>Entry point for the Example 01 OpenGL Raylib example.</summary>
public abstract class Program
{
    /// <summary>Starts the example application.</summary>
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);
        Example01.Example01 shared = new(gui);
        win.RunGui(shared.Draw);
    }
}

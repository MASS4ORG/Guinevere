using Guinevere;

namespace Example01OpenGLOpenTK;

/// <summary>Entry point for the Example 01 OpenGL OpenTK example.</summary>
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

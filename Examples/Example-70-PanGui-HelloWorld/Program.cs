using Guinevere;

namespace Example70PanGuiHelloWorld;

/// <summary>Entry point for the Example 70 PanGui HelloWorld example.</summary>
public abstract class Program
{
    /// <summary>Starts the example application.</summary>
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 800, 600, "Hello World!");

        win.RunGui(() => Draw(gui));
    }

    static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 29, 29, 29));
        gui.DrawText("Hello, world!", size: 32, color: Color.White);
    }
}

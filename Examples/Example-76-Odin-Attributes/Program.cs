using Guinevere;

namespace Example_76_Odin_Attributes;

/// <summary>
/// An attribute-by-attribute tour of Autoformers in the style of a well-known inspector's attribute gallery: every
/// page shows the live form Autoformers builds beside the real source of its demo class.
/// </summary>
public static class Program
{
    /// <summary>Opens the tour window.</summary>
    public static void Main()
    {
        var gui = new Gui { ControlPalette = ControlPalette.Light };
        var tour = new AttributeTour();

        using var win = new GuiWindow(gui, 1600, 900, "Autoformers: Attributes");
        win.RunGui(() => tour.Draw(gui));
    }
}

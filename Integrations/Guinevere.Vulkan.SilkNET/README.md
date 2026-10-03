# Guinevere.Vulkan.SilkNET

Modern Vulkan API using [Silk.NET](https://github.com/dotnet/Silk.NET).

## Basic Usage

```csharp
using Guinevere;

namespace Example;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);

        win.RunGui(() =>
        {
            gui.DrawRect(gui.ScreenRect, Color.Blue);
            gui.DrawText("Hello, world!");
        });
    }
}
```

## Linux window selection

To use X11/XWayland for a custom application bar, set this preference before creating the first GLFW window:

```csharp
Environment.SetEnvironmentVariable("SILKNET_USE_WAYLAND", "0");
using var window = new GuiWindow(gui);
```

This Guinevere integration translates the setting into GLFW's X11 initialization hint. It requires an available
X11/XWayland display; other values leave GLFW's default selection in place. Native Wayland cannot provide the
desktop-coordinate movement used by AppBar, so its automatic native-decoration fallback stays enabled.
Window-system selection applies at startup; native decorations can be changed later.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

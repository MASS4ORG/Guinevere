# Guinevere.Vulkan.SilkNET

Modern Vulkan API using [Silk.NET](https://github.com/dotnet/Silk.NET).

## Basic Usage

```csharp
using Guinevere;
using Guinevere.Vulkan.SilkNET;

namespace Sample;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);

        win.RunGui(() =>
        {
            gui.DrawRect(gui.ScreenRect, Blue;
            gui.DrawText("Hello, world!");
        });
    }
}
```

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

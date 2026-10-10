# Guinevere.OpenGL.SilkNET

OpenGL API using [Silk.NET](https://github.com/dotnet/Silk.NET).

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

## Rendering and frame pacing

Skia draws on the GPU into the window's default framebuffer. When a Skia GL context cannot be created, Skia rasterizes on the CPU and the frame is uploaded as a texture; `GUINEVERE_RENDERER=raster` forces that path. `CanvasRenderer.IsGpuAccelerated` reports which one runs.

The window renders on demand by default: it waits for input between frames. It renders a few frames after each input event, continuously while a button or key is held, whenever keyed animations, style transitions and timed controls (tooltips, toasts, caret blink, indeterminate progress) are moving, and at least every `Pacing.MaxIdleSeconds` (0.5 s). Content that changes without input, such as a playing game viewport or progress from a background task, calls `gui.RequestFrame()` (safe from any thread) or `gui.RequestFrameIn(seconds)` for a timer. Games and other content that changes every frame can render continuously instead:

```csharp
using var win = new GuiWindow(gui);
win.Pacing.OnDemand = false;
```

`GUINEVERE_PACING=on-demand` or `continuous` overrides the setting at run time.

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

# Guinevere.OpenGL.Raylib

OpenGL API using [Raylib-cs](https://github.com/ChrisDill/Raylib-cs).

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

Skia draws on the GPU into Raylib's default framebuffer. When a Skia GL context cannot be created, Skia rasterizes on the CPU and the frame is uploaded as a Raylib texture; `GUINEVERE_RENDERER=raster` forces that path. `CanvasRenderer.IsGpuAccelerated` reports which one runs.

The window renders on demand by default: it waits for input between frames, and `win.Pacing.OnDemand = false` renders continuously instead. On demand, it renders a few frames after each input event, while a mouse button is held, whenever keyed animations, style transitions and timed controls are moving, and at least every `Pacing.MaxIdleSeconds` (0.5 s). Content that changes without input calls `gui.RequestFrame()` or `gui.RequestFrameIn(seconds)`. Raylib has no timed event wait, so the idle window sleeps and polls input every 8 ms; requests from other threads are picked up at the next poll. `GUINEVERE_PACING=on-demand` or `continuous` overrides the setting at run time.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

# Sample-70-PanGui-HelloWorld

The simplest possible Guinevere application. It demonstrates the absolute minimum code required to get a window running and display some text.

## Code Overview
```csharp
var gui = new Gui();
using var win = new GuiWindow(gui);

win.RunGui(() => {
    gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 29, 29, 29));
    gui.DrawText("Hello, world!");
});
```

## Features
- Window initialization.
- Basic frame loop.
- Background clearing.
- Simple text rendering.

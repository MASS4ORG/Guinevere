# Guinevere

![guinevere](/guinevere-badge.png)

[![CI](https://github.com/mass4org/guinevere/actions/workflows/ci.yml/badge.svg)](https://github.com/brmassa/guinevere/actions/workflows/ci.yml)
[![Release](https://github.com/mass4org/guinevere/actions/workflows/release.yml/badge.svg)](https://github.com/brmassa/guinevere/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/org.mass4.Guinevere.svg)](https://www.nuget.org/packages/org.mass4.Guinevere/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A **GPU accelerated immediate mode GUI system** built on SkiaSharp, designed for high-performance applications with modern graphics APIs support. You can use it to create rich and beautiful apps.

> **Important**
>
> Guinevere is a very new library. While an earlier iteration is actively used within the Turian Game Engine, this specific library hasn't yet established a track record of reliability in production environments.

## Highlights

- **Cross-Platform: Windows, Linux & Mac**
- **Immediate Mode**
- **100% C# with Latest .NET**
- **GPU Accelerated Rendering**
- **Fluent API**
- **Multiple Graphics API Support**
- **Multiple Framework Integrations**

| Integration        | Graphics API | C# Framework | Use Case                                    |
|--------------------|--------------|--------------|---------------------------------------------|
| **Vulkan.SilkNET** | Vulkan       | Silk.NET     | Maximum performance, modern graphics        |
| **OpenGl.SilkNET** | OpenGL       | Silk.NET     | High-performance applications (Recommended) |
| **OpenGl.OpenTK**  | OpenGL       | OpenTK       | Game development, tools                     |
| **OpenGl.Raylib**  | OpenGL       | Raylib-cs    | Simple games, prototypes                    |

## Table of Contents

- [Highlights](#highlights)
- [Quick Start](#quick-start)
- [Features](#features)
  - [Layout](#layout)
  - [Interactive Elements](#interactive-elements)
  - [Text & Styling](#text--styling)
  - [Animation](#animation)
  - [Shapes & Effects](#shapes--effects)
  - [Scrolling & Clipping](#scrolling--clipping)
  - [Advanced Controls](#advanced-controls)
  - [Performance](#performance)
- [Samples](#samples)
- [License](#license)
- [Acknowledgments](#acknowledgments)

## Quick Start

### Starting a New App

1. **Create a new .NET project:**

   ```powershell
   dotnet new console -n MyGuinevereApp
   cd MyGuinevereApp
   ```

2. **Install Guinevere and an integration package:**

   ```powershell
   dotnet add package org.mass4.Guinevere
   dotnet add package org.mass4.Guinevere.OpenGL.SilkNET
   ```

3. **Basic Usage:**

   ```csharp
   using Guinevere;
   using Guinevere.OpenGL.SilkNET;

   public abstract class Program
   {
       public static void Main()
       {
           var gui = new Gui();
           using var win = new GuiWindow(gui);

           win.RunGui(() =>
           {
               gui.DrawRect(gui.ScreenRect, Color.mass4org);
               gui.DrawText("Hello, world!");
           });
       }
   }
   ```

---

## Features

### Layout

- Flexible box model: margins, padding, gaps, alignment
- Horizontal/vertical flow with `Direction(Axis.Horizontal|Vertical)`
- Responsive sizing: `Expand()`, `ExpandWidth()`, `ExpandHeight()`
- Content alignment with `AlignContent(x, y)`

  ```csharp
  // Flexible layout with alignment and spacing
  using (gui.Node().Expand()
      .Direction(Axis.Horizontal)
      .Gap(20)
      .Margin(30)
      .Padding(15)
      .AlignContent(0.5f, 0.2f)
      .Enter())
  {
      // Children laid out horizontally with gaps
  }

  // Responsive sizing
  gui.Node().ExpandWidth().Height(100).Margin(10, 20).Padding(5, 10, 15, 20);
  ```

### Interactive Elements

- Built-in controls: buttons, checkboxes, toggles, dropdowns
- Text input variants: single-line, password, multiline
- Focus management and keyboard navigation
- Custom interactables for arbitrary shapes/regions

  ```csharp
  // Common controls
  if (gui.Button("Click me!")) { /* ... */ }
  gui.Checkbox(ref checkbox, "Enable feature");
  gui.Toggle(ref toggle, "Dark mode");
  gui.Dropdown(options, ref dropdown);

  // Text inputs
  textInput = gui.TextInput(textInput);
  passwordInput = gui.PasswordInput(passwordInput);
  textArea = gui.TextArea(textArea);

  // Custom interactive area with shape
  var interactable = gui.GetInteractable();
  if (interactable.OnHover()) gui.DrawBackgroundRect(Color.LightBlue);
  if (interactable.OnClick()) Console.WriteLine("Clicked!");

  var shape = Shape.Circle(50);
  var shaped = gui.GetInteractable(position, shape);
  ```

### Text & Styling

- Rich text rendering with Unicode and emoji
- Wrapping, sizes, and color control
- Theming via transient color changes

  ```csharp
  gui.DrawText("Title", 24, Color.White);
  gui.DrawText("Wrapped text", 12, Color.Gray, wrapWidth: 300);
  gui.SetTextColor(Color.Red); // all text from now on will be red by default
  gui.DrawText("Red text");
  gui.SetTextColor(Color.White);
  ```

### Animation

- Smooth value/state transitions
- `AnimationFloat` and `AnimateBool01()` helpers
- Easing functions: `Linear`, `EaseIn/Out`, `SmoothStep`, `BackOut`, `ElasticOut`, `BounceOut`
- Live metrics: `ActiveAnimationCount`

  ```csharp
  var fade = gui.AnimateBool01(isVisible, 0.5f, Easing.SmoothStep);
  var animFloat = gui.GetAnimationFloat(value);
  animFloat.AnimateTo(target, 0.3f, Easing.ElasticOut);
  var v = animFloat.GetValue();

  // Status
  gui.DrawText($"Active animations: {gui.ActiveAnimationCount}", 12, Color.White);
  ```

### Shapes & Effects

- Basic shapes: circle, rectangle, arc
- Fills: solid, linear/radial gradients
- Borders and rounded corners
- Shadows: inner/outer with blur and spread
- Shape operations: union, subtract, intersect, transform

  ```csharp
  // Basic shape & gradient
  gui.DrawShape(center, Shape.Circle(100))
      .LinearGradientColor(Color.Red, Color.Blue, Angle.Degrees(45))
      .InnerShadow(Color.Black, new Vector2(0, 2), blur: 5, spread: -2)
      .OuterShadow(Color.Gray, new Vector2(2, 2), blur: 8, spread: 0);

  // Composition
  var combined = Shape.Circle(60) + Shape.Rectangle(80, 40);
  gui.DrawShape(center, combined);

  // Simple rect helpers
  gui.DrawBackgroundRect(Color.Blue, radius: 10);
  // gui.DrawRectBorder(rect, Color.Red, thickness: 2);
  ```

### Scrolling & Clipping

- Scrollbars: `ScrollY()` / `ScrollX()`
- Clip content to node bounds: `ClipContent()`
- Programmatic control: `ScrollToTop()`, `SetScrollPercentage()`
- Custom clip shapes with `SetClipArea()`

  ```csharp
  using (gui.Node(400, 300).Enter())
  {
      gui.ScrollY(Color.Black, Color.Gray);
      gui.ClipContent();

      for (int i = 0; i < 50; i++)
          gui.DrawText($"Line {i + 1}", 14, Color.White);
  }

  // Programmatic control
  gui.ScrollToTop("scrollNodeId");
  gui.SetScrollPercentage("scrollNodeId", Axis.Vertical, 0.5f);

  // Custom clip area
  gui.SetClipArea(gui.CurrentNode, Shape.Circle(100));
  ```

### Advanced Controls

- Tabs: horizontal, pill, and vertical variants
- Menus, dropdowns, and window chrome utilities
- Z-index layering and transforms

  ```csharp
  private static int activeTab = 0;

  gui.Tabs(ref activeTab, tabs =>
  {
      tabs.Tab("Home", () => gui.DrawText("Home content", 12, Color.Gray));
      tabs.Tab("Settings", () => gui.DrawText("Settings content", 12, Color.Gray));
      tabs.Tab("About", () => gui.DrawText("About content", 12, Color.Gray));
  });

  // Window UI & transforms
  gui.DrawWindowTitlebar();
  gui.SetZIndex(10);
  // gui.SetTransform(Matrix3x2.CreateScale(1.2f, 1.2f, center));
  ```

### Performance

- GPU-accelerated rendering via SkiaSharp
- Immediate mode with minimal memory overhead
- Dirty-flag updates and multi-pass rendering
- Real-time metrics: FPS, frame count, delta time

  ```csharp
  // Pass-based work split
  if (gui.Pass == Pass.Pass1Layout)
  {
      // Layout calculations
  }
  else if (gui.Pass == Pass.Pass2Render)
  {
      // Rendering only
  }

  // Metrics
  gui.DrawText($"FPS: {gui.Time.SmoothFps:F1}", 12, Color.White);
  gui.DrawText($"Frame: {gui.Time.Frames}", 12, Color.White);
  gui.DrawText($"Delta: {gui.Time.DeltaTime * 1000:F1}ms", 12, Color.White);
  ```

## Samples

The repository includes comprehensive [Samples](/Samples) demonstrating various features.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [SkiaSharp](https://github.com/mono/SkiaSharp): The foundation of our rendering system
- [OpenTK](https://github.com/opentk/opentk): OpenGL bindings for .NET
- [Raylib-cs](https://github.com/ChrisDill/Raylib-cs): C# bindings
- [Silk.NET](https://github.com/dotnet/Silk.NET): Modern .NET bindings for graphics APIs
- [NUKE](https://nuke.build): Build automation system
- [PanGui](https://pangui.io/): Inspiration for the API
- [Prowl.Paper](https://github.com/ProwlEngine/Prowl.Paper): Inspiration for the API

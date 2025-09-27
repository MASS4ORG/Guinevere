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

- [Highlights](#highlights)
- [Quick Start](#quick-start)
  - [Basic Usage](#basic-usage)
- [Features](#features)
  - [Layout](#layout)
  - [Vertical Layout & Alignment](#vertical-layout--alignment)
  - [Interactive Elements](#interactive-elements)
  - [Text & Unicode](#text--unicode)
  - [Text Wrapping & Styling](#text-wrapping--styling)
  - [Rainbow Text Effect](#rainbow-text-effect)
  - [Animation](#animation)
  - [Smooth Value Animation](#smooth-value-animation)
  - [Hover Animations](#hover-animations)
  - [Shape](#shape)
  - [Shape Operations](#shape-operations)
  - [Shape Transformations](#shape-transformations)
  - [Advanced Shape Effects](#advanced-shape-effects)
  - [Basic Controls](#basic-controls)
  - [Tab Controls](#tab-controls)
  - [Layout System](#layout-system)
  - [Animation System](#animation-system)
  - [Interactive Elements](#interactive-elements)
  - [Visual Effects](#visual-effects)

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
- Horizontal/vertical layouts:
  ```csharp
  using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(10).Enter()) { /* ... */ }
  using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter()) { /* ... */ }
  ```
- Responsive sizing: `Expand()`, `ExpandWidth()`, `ExpandHeight()`
- Content alignment: `AlignContent(0.5f, 0.5f)`

### Interactive Elements
- Buttons, checkboxes, toggles, dropdowns:
  ```csharp
  if (gui.Button("Click me!")) { /* ... */ }
  gui.Checkbox(ref checkbox, "Enable feature");
  gui.Toggle(ref toggle, "Dark mode");
  gui.Dropdown(options, ref dropdown);
  ```
- Text input, password, text area:
  ```csharp
  textInput = gui.TextInput(textInput);
  passwordInput = gui.PasswordInput(passwordInput);
  textArea = gui.TextArea(textArea);
  ```
- Focus and keyboard navigation

### Text & Styling
- Rich text rendering, Unicode, emoji
- Wrapping and custom colors:
  ```csharp
  gui.DrawText("Title", 24, Color.White);
  gui.DrawText("Wrapped text", 12, Color.Gray, wrapWidth: 300);
  gui.SetTextColor(Color.Red);
  gui.DrawText("Red text");
  gui.SetTextColor(Color.White);
  ```

### Animation
- Smooth value and state transitions
- Easing functions: `Linear`, `SmoothStep`, `ElasticOut`, etc.
  ```csharp
  var fade = gui.AnimateBool01(isVisible, 0.5f, Easing.SmoothStep);
  var animFloat = gui.GetAnimationFloat(value);
  animFloat.AnimateTo(target, 0.3f, Easing.ElasticOut);
  ```

### Shapes & Effects
- Draw circles, rectangles, gradients, shadows:
  ```csharp
  gui.DrawShape(center, Shape.Circle(50)).SolidColor(Color.Red);
  gui.DrawShape(center, Shape.Rectangle(100, 60)).LinearGradientColor(Color.Red, Color.Blue);
  ```
- Shape operations: union, subtract, intersect
  ```csharp
  var combined = Shape.Circle(60) + Shape.Rectangle(80, 40);
  gui.DrawShape(center, combined);
  ```

### Advanced Controls
- Tabs, pill tabs, vertical tabs:
  ```csharp
  gui.Tabs(ref activeTab, tabs => { tabs.Tab("Home"); tabs.Tab("Settings"); });
  gui.PillTabs(ref activeTab, tabs => { tabs.Tab("Overview"); });
  gui.VerticalTabs(ref activeTab, tabs => { tabs.Tab("Account"); });
  ```
- Scrollable content:
  ```csharp
  using (gui.Node(400, 300).Enter()) { gui.ScrollY(); gui.ClipContent(); }
  ```
- Clipping, z-index, transforms

### Performance
- GPU-accelerated rendering
- Dirty flag system for efficient updates
- Multi-pass rendering
- Real-time metrics:
  ```csharp
  gui.DrawText($"FPS: {gui.Time.SmoothFps:F1}", 12, Color.White);
  gui.DrawText($"Frame: {gui.Time.Frames}", 12, Color.White);
  ```

---

## Advanced Features

- **Custom Shape Drawing**: Draw rectangles, circles, triangles, borders, gradients, shadows, and rounded corners.
  ```csharp
  gui.DrawRect(rect, Color.Blue, radius: 10);
  gui.DrawCircle(center, 50, Color.Red);
  gui.DrawTriangle(a, b, c, Color.Green);
  gui.DrawRectBorder(rect, Color.Black, thickness: 2);
  ```
- **Clipping**: Clip content to any shape, not just rectangles.
  ```csharp
  gui.SetClipArea(node, Shape.Circle(100));
  ```
- **Custom Interactable Areas**: Create interactive regions with arbitrary shapes.
  ```csharp
  var interactable = gui.GetInteractable(position, Shape.Circle(50));
  if (interactable.OnClick()) { /* ... */ }
  ```
- **Line Drawing**: Draw custom lines and connectors.
  ```csharp
  gui.DrawLine(start, end, Color.Gray, thickness: 2);
  ```
- **Background Drawing**: Easy backgrounds with radius and color.
  ```csharp
  gui.DrawBackgroundRect(Color.LightGray, radius: 8);
  ```
- **Extensive Easing Functions**: Use advanced animation curves for smooth, springy, or bouncy transitions.
  ```csharp
  var fade = gui.AnimateBool01(isVisible, 0.5f, Easing.BounceInOut);
  var spring = Easing.Spring(t, dampingRatio: 0.3f, angularFrequency: 15f);
  ```
- **Spring Animation**: Physically-inspired spring transitions with damping and frequency control.
  ```csharp
  var springValue = Easing.Spring(t);
  ```
- **Hierarchical Layout System**: Stack-based layout nodes for nested, responsive UI.
  ```csharp
  using (gui.Node().Expand().Enter()) { /* children nodes */ }
  ```
- **Node Identification**: Uniquely identify and reference layout nodes.
  ```csharp
  var id = gui.NodeId(filePath, lineNumber);
  ```
- **Layout Calculation**: Automatic recalculation for responsive UIs.
  ```csharp
  gui.CalculateLayout();
  ```
- **Flexible Sizing**: Absolute or relative sizing, with margins, padding, and alignment.
  ```csharp
  gui.Node(100, 50).Margin(10).Padding(5).AlignContent(0.5f);
  ```
- GPU-accelerated rendering
- Dirty flag system for efficient updates
- Multi-pass rendering
- Real-time metrics:
  ```csharp
  gui.DrawText($"FPS: {gui.Time.SmoothFps:F1}", 12, Color.White);
  gui.DrawText($"Frame: {gui.Time.Frames}", 12, Color.White);
  ```

### Tab Controls

```csharp
private static int activeTab = 0;

// Horizontal tabs
gui.Tabs(ref activeTab, tabs =>
{
    tabs.Tab("Home", () => gui.DrawText("Home content", 12, Color.Gray));
    tabs.Tab("Settings", () => gui.DrawText("Settings content", 12, Color.Gray));
    tabs.Tab("About", () => gui.DrawText("About content", 12, Color.Gray));
});

// Pill tabs
gui.PillTabs(ref activeTab, tabs =>
{
    tabs.Tab("Overview");
    tabs.Tab("Details");
}, activeTabColor: Color.Blue);
```

## Features

Guinevere provides many advanced features for creating rich user interfaces:

### Layout System

Flexible box model with margins, padding, gaps, and alignment

```csharp
// Flexible layout with alignment
using (gui.Node().Expand()
    .Direction(Axis.Horizontal)    // Horizontal layout
    .Gap(20)                       // 20px between children
    .Margin(30)                    // 30px margin all sides
    .Padding(15)                   // 15px padding inside
    .AlignContent(0.5f, 0.2f)      // Center horizontally, 20% from top
    .Enter())
{
    // Content here will be laid out horizontally with gaps
}


// Responsive sizing
gui.Node().ExpandWidth()           // Take available width
    .Height(100)                   // Fixed height
    .Margin(10, 20)               // 10px vertical, 20px horizontal margin
    .Padding(5, 10, 15, 20);      // Top, right, bottom, left padding
```

- Horizontal/Vertical directions with `Direction(Axis.Horizontal/Vertical)`
- Content alignment with `AlignContent(horizontal, vertical)`
- Responsive sizing with `Expand()`, `ExpandWidth()`, `ExpandHeight()`
- Spacing control with `Margin()`, `Padding()`, `Gap()`

### Animation System

```csharp
// Boolean to 0-1 animation with easing
var fadeAmount = gui.AnimateBool01(isVisible, 0.5f, Easing.SmoothStep);

// Float value animation
var animFloat = gui.GetAnimationFloat(currentValue);
animFloat.AnimateTo(targetValue, 0.3f, Easing.ElasticOut);
var value = animFloat.GetValue();
```

```csharp
// Available easing functions:
// Easing.Linear, Easing.EaseIn, Easing.EaseOut, Easing.SmoothStep
// Easing.BackOut, Easing.ElasticOut, Easing.BounceOut
```

- **Animation System**: Smooth, performant animations

  - `AnimationFloat` for smooth value transitions
  - `AnimateBool01()` for boolean state animations
  - Multiple easing functions: `Linear`, `EaseIn/Out`, `SmoothStep`, `BackOut`, `ElasticOut`
  - Real-time animation tracking with `ActiveAnimationCount`

```csharp
// Check animation status
gui.DrawText($"Active animations: {gui.ActiveAnimationCount}", 12, Color.White);
```

### Interactive Elements

- **Text Rendering**: Rich text display and input

  - Multiple font sizes and colors in `DrawText()`
  - Text wrapping with `wrapWidth` parameter
  - Unicode and emoji support
  - Input controls: `TextInput()`, `PasswordInput()`, `TextArea()`

```csharp
// Custom interactive areas
var interactable = gui.GetInteractable();
if (interactable.OnHover())
    gui.DrawBackgroundRect(Color.LightBlue);
if (interactable.OnClick())
    Console.WriteLine("Clicked!");
if (interactable.OnHold())
    Console.WriteLine("Holding...");

// Interactive with custom shape
var shape = Shape.Circle(50);
var shapeInteractable = gui.GetInteractable(position, shape);
```

- **Advanced Controls**: Professional UI components

  - Tabbed interfaces with `Tabs()`, `PillTabs()`, `VerticalTabs()`
  - Scrollable content with `ScrollY()`, `ScrollX()`
  - Clipping with `ClipContent()` and `SetClipArea()`
  - Window chrome with `DrawWindowTitlebar()`

- **Interactive Elements**: Complete input handling

  - `GetInteractable()` for custom interactive areas
  - Built-in controls: buttons, checkboxes, toggles, dropdowns
  - Hover, click, and hold state detection
  - Focus management for keyboard navigation

### Visual Effects

```csharp
// Background with radius
gui.DrawBackgroundRect(Color.Blue, radius: 10);

// Border
gui.DrawRectBorder(rect, Color.Red, thickness: 2);

// Gradients and shadows with shapes
gui.DrawShape(center, Shape.Circle(100))
    .LinearGradientColor(Color.Red, Color.Blue, Angle.Degrees(45))
    .RadialGradientColor(Color.White, Color.Black, 0, 50)
    .InnerShadow(Color.Black, new Vector2(0, 2), blur: 5, spread: -2)
    .OuterShadow(Color.Gray, new Vector2(2, 2), blur: 8, spread: 0);
```

- Border radius with `radius` parameter
- Background colors and gradients
- Shadow effects: `InnerShadow()`, `OuterShadow()`
- Linear and radial gradients: `LinearGradientColor()`, `RadialGradientColor()`
- **Shape System**: Advanced shape rendering and manipulation
  - Basic shapes: `Shape.Circle()`, `Shape.Rect()`, `Shape.Arc()`
  - Shape operations: `Union()`, `Expand()`, `Rotate()`, `Scale()`, `Mix()`
  - Complex shapes with `DrawShape()` and gradient fills

### Scrolling and Clipping

```csharp
using (gui.Node(400, 300).Enter())
{
    gui.ScrollY(Color.Black, Color.Gray);  // Add vertical scrollbar
    gui.ClipContent();                     // Clip content to bounds

    // Long content that will scroll
    for (int i = 0; i < 50; i++)
    {
        gui.DrawText($"Line {i + 1}", 14, Color.White);
    }
}

// Programmatic scrolling
gui.ScrollToTop("scrollNodeId");
gui.SetScrollPercentage("scrollNodeId", Axis.Vertical, 0.5f);
```

### Advanced Controls

```csharp
// Window title bar
gui.DrawWindowTitlebar();

// Custom clipping area
gui.SetClipArea(gui.CurrentNode, Shape.Circle(100));

// Z-index layering
gui.SetZIndex(10);  // Higher values render on top

// Transform matrix
gui.SetTransform(Matrix3x2.CreateScale(1.2f, 1.2f, center));
```

### Performance Features

```csharp
// Check rendering passes
if (gui.Pass == Pass.Pass1Layout)
{
    // Layout calculations
}
else if (gui.Pass == Pass.Pass2Render)
{
    // Rendering only
}
```

Optimized for real-time applications

- Dirty flag system for efficient updates
- GPU-accelerated rendering via SkiaSharp
- Immediate mode architecture for minimal memory overhead
- Multi-pass rendering system

```csharp
// Performance metrics
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

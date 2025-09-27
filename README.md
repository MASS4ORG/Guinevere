# Guinevere

[![CI](https://github.com/mass4org/guinevere/actions/workflows/ci.yml/badge.svg)](https://github.com/brmassa/guinevere/actions/workflows/ci.yml)
[![Release](https://github.com/mass4org/guinevere/actions/workflows/release.yml/badge.svg)](https://github.com/brmassa/guinevere/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/org.mass4.Guinevere.svg)](https://www.nuget.org/packages/org.mass4.Guinevere/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A **GPU accelerated immediate mode GUI system** built on SkiaSharp, designed for high-performance applications with modern graphics APIs support. You can use it to create rich and beautiful apps.

> **Important**
>
> Guinevere is a very new library. While an earlier iteration is actively used within the Turian Game Engine, this specific library hasn't yet established a track record of reliability in production environments.

## Features

- **Cross-Platform: Windows, Linux & Mac**
- **Immediate Mode**
- **100% C# with Latest .NET**
- **GPU Accelerated Rendering**
- **Fluent API**
- **Multiple Graphics API Support**
- **Multiple Framework Integrations**

| Package            | Graphics API | C# Framework | Use Case                                    |
|--------------------|--------------|--------------|---------------------------------------------|
| **Vulkan.SilkNET** | Vulkan       | Silk.NET     | Maximum performance, modern graphics        |
| **OpenGl.SilkNET** | OpenGL       | Silk.NET     | High-performance applications (Recommended) |
| **OpenGl.OpenTK**  | OpenGL       | OpenTK       | Game development, tools                     |
| **OpenGl.Raylib**  | OpenGL       | Raylib-cs    | Simple games, prototypes                    |

## Quick Start

### Basic Usage

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

### Layout

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
            // Background and basic shapes
            gui.DrawRect(gui.ScreenRect, Color.DarkGray);
            gui.DrawRect(new(0, 0, 150, 150), Color.DeepPink);
            gui.DrawCircle(new(75, 75), 35, Color.BlueViolet);

            // Horizontal layout with gaps and margins
            using (gui.Node().Expand()
                       .Margin(100)
                       .Direction(Axis.Horizontal)
                       .Gap(10)
                       .AlignContent(0.5f)
                       .Enter())
            // Simple horizontal layout
            using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Margin(50).Enter())
            {
                gui.DrawBackgroundRect(Color.Crimson, radius: 20);

                // Nested container with padding
                using (gui.Node(150, 100)
                           .Margin(10, 20)
                           .Padding(15)
                           .Enter())
                using (gui.Node(100, 100).Enter())
                {
                    gui.DrawBackgroundRect(Color.Green, radius: 8);
                    gui.DrawText("Nested", Color.White);
                    gui.DrawBackgroundRect(Color.Red, radius: 10);
                    gui.DrawText("Box 1", Color.White);
                }

                // Vertical layout inside horizontal parent
                using (gui.Node(200, 200)
                           .Direction(Axis.Vertical)
                           .AlignContent(0.5f)
                           .Gap(5)
                           .Enter())

                using (gui.Node(100, 100).Enter())
                {
                    gui.DrawBackgroundRect(Color.Purple, radius: 15);

                    using (gui.Node(80, 30).Enter())
                    {
                        gui.DrawBackgroundRect(Color.Orange);
                        gui.DrawText("Item 1", Color.Black);
                    }

                    using (gui.Node(80, 30).Enter())
                    {
                        gui.DrawBackgroundRect(Color.Yellow);
                        gui.DrawText("Item 2", Color.Black);
                    }
                    gui.DrawBackgroundRect(Color.Green, radius: 10);
                    gui.DrawText("Box 2", Color.White);
                }
            }
        });
    }
}
```


### Vertical Layout & Alignment

```csharp
using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Padding(20).Enter())
{
    gui.DrawText("Header", 20, Color.White);
    using (gui.Node().Height(50).Enter())
    {
        gui.DrawBackgroundRect(Color.Blue);
        gui.DrawText("Content", Color.White);
    }
    gui.DrawText("Footer", 12, Color.Gray);
}

// Centered content
using (gui.Node().Expand().AlignContent(0.5f, 0.5f).Enter())
{
    using (gui.Node(200, 100).Margin(20).Padding(15).Enter())
    {
        gui.DrawBackgroundRect(Color.Purple, radius: 8);
        gui.DrawText("Centered Box", Color.White);
    }
}
```

### Alignment and Spacing

```csharp
using (gui.Node().Expand().AlignContent(0.5f, 0.5f).Enter()) // Center content
{
    using (gui.Node(200, 100).Margin(20).Padding(15).Enter())
    {
        gui.DrawBackgroundRect(Color.Purple, radius: 8);
        gui.DrawText("Centered Box", Color.White);
    }
}
```




```csharp
using (gui.Node(120, 40).Enter())
{
    var interactable = gui.GetInteractable();
    var bgColor = interactable.OnHover() ? Color.LightBlue : Color.Blue;
    gui.DrawBackgroundRect(bgColor, radius: 5);
    gui.DrawText("Click me!", Color.White);
    if (interactable.OnClick())
        Console.WriteLine("Button clicked!");
}
```


### Text & Unicode

```csharp
gui.DrawText("Large Title", 24, Color.White);
gui.DrawText("Subtitle with custom color", 16, Color.LightBlue);
gui.DrawText("Small text with wrapping that demonstrates how text can wrap to multiple lines when a wrap width is specified", 12, Color.Gray, wrapWidth: 400);
gui.DrawText("Unicode: 🚩❓💣★ ♥ ♦ ♣ ♠", 14, Color.Yellow);
gui.DrawText("Symbols: !@#$%^&*()_+-=", 12, Color.Cyan);
```

                gui.DrawText("Text Area:", 14, Color.White);
                textArea = gui.TextArea(textArea, width: 400, height: 100, placeholder: "Multiline text...");


### Text Wrapping & Styling

```csharp
gui.DrawText($"Current text: '{textInput}'", 12, Color.LightGray);
gui.DrawText($"Password length: {passwordInput.Length} chars", 12, Color.LightGray);
gui.DrawText($"Text area lines: {textArea.Split('\n').Length}", 12, Color.LightGray);
gui.DrawText("This long text will wrap to multiple lines when wrapWidth is specified", 12, Color.White, wrapWidth: 300);
gui.SetTextColor(Color.Red);
gui.DrawText("Red text using SetTextColor");
gui.SetTextColor(Color.White); // Reset
```


### Rainbow Text Effect

```csharp
for (int i = 0; i < 5; i++)
{
    var hue = (i * 60) % 360;
    var color = Color.FromArgb(255,
        (int)(Math.Sin(hue * Math.PI / 180) * 127 + 128),
        (int)(Math.Sin((hue + 120) * Math.PI / 180) * 127 + 128),
        (int)(Math.Sin((hue + 240) * Math.PI / 180) * 127 + 128));
    gui.DrawText($"Rainbow line {i + 1}", 12, color);
}
```

### Dynamic Text Styling

```csharp
gui.SetTextColor(Color.Red);
gui.DrawText("This text is red");
gui.SetTextColor(Color.White); // Reset

// Rainbow effect
for (int i = 0; i < 5; i++)
{
    var hue = (i * 60) % 360;
    var color = Color.FromArgb(255,
        (int)(Math.Sin(hue * Math.PI / 180) * 127 + 128),
        (int)(Math.Sin((hue + 120) * Math.PI / 180) * 127 + 128),
        (int)(Math.Sin((hue + 240) * Math.PI / 180) * 127 + 128));
    gui.DrawText($"Rainbow line {i + 1}", 12, color);
}
    }
}
```

### Animation

```csharp
using Guinevere;
using Guinevere.OpenGL.SilkNET;
using System.Numerics;

public abstract class Program
{
    private static bool showAnimatedElement = false;
    private static bool showElement = false;
    private static float sliderValue = 0.5f;
    private static AnimationFloat popupVisibility;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);

        win.RunGui(() =>
        {
            gui.DrawRect(gui.ScreenRect, Color.FromArgb(32, 32, 48));

            using (gui.Node().Expand().Margin(20).Gap(20).Direction(Axis.Vertical).Enter())
            // Simple fade animation
            var alpha = gui.AnimateBool01(showElement, 0.5f, Easing.SmoothStep);

            if (gui.Button("Toggle Element"))
                showElement = !showElement;

            if (alpha > 0.001f)
            {
                gui.DrawText($"Animation Demo - FPS: {gui.Time.SmoothFps:F1}", 18, Color.White);
                gui.DrawText($"Active Animations: {gui.ActiveAnimationCount}", 14, Color.Gray);

                // Toggle button for animated element
                using (gui.Node(150, 40).Enter())
                using (gui.Node(200, 50).Enter())
                {
                    var buttonInteractable = gui.GetInteractable();
                    var hoverAmount = gui.AnimateBool01(buttonInteractable.OnHover(), 0.2f, Easing.SmoothStep);
                    var bgColor = Color.FromArgb((int)(255 * (0.3f + hoverAmount * 0.3f)), 70, 130, 180);

                    gui.DrawBackgroundRect(bgColor, 8);
                    gui.DrawText(showAnimatedElement ? "Hide Element" : "Show Element", 12, Color.White);

                    if (buttonInteractable.OnClick())
                    {
                        showAnimatedElement = !showAnimatedElement;
                    }
                }

                // Animated element with different easing functions
                var elementAlpha = gui.AnimateBool01(showAnimatedElement, 0.5f, Easing.ElasticOut);
                if (elementAlpha > 0.001f)
                {
                    using (gui.Node(200, 80).Enter())
                    {
                        var animatedColor = Color.FromArgb((int)(255 * elementAlpha), 74, 226, 74);
                        gui.DrawBackgroundRect(animatedColor, 10);
                        gui.DrawText("Animated Element!", 14, Color.FromArgb((int)(255 * elementAlpha), 255, 255, 255));
                    }
                    var color = Color.FromArgb((int)(255 * alpha), 74, 226, 74);
                    gui.DrawBackgroundRect(color);
                    gui.DrawText("Animated!", Color.White);
                }
            }
        });
    }
}
```

```csharp
// Animated slider
using (gui.Node().Height(60).Direction(Axis.Vertical).Gap(10).Enter())
{
    gui.DrawText($"Animated Slider: {sliderValue:F2}", 14, Color.White);

    using (gui.Node(300, 20).Enter())
    {
        var sliderInteractable = gui.GetInteractable();

        if (sliderInteractable.OnHold())
        {
            var mouseX = gui.Input.MousePosition.X;
            var relativeX = mouseX - gui.CurrentNode.Rect.X;
            sliderValue = Math.Clamp(relativeX / gui.CurrentNode.Rect.W, 0f, 1f);
        }

        // Animate the visual representation
        var animatedValue = gui.GetAnimationFloat(sliderValue);
        animatedValue.AnimateTo(sliderValue, 0.3f, Easing.SmoothStep);
        var fillWidth = gui.CurrentNode.Rect.W * animatedValue.GetValue();

        // Background
        gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(100, 100, 100), 10);

        // Animated fill
        if (fillWidth > 0)
        {
            var fillRect = new Rect(gui.CurrentNode.Rect.X, gui.CurrentNode.Rect.Y, fillWidth, gui.CurrentNode.Rect.H);
            gui.DrawRect(fillRect, Color.FromArgb(74, 144, 226), 10);
        }
    }
}
```

### Smooth Value Animation

```csharp
// Easing function demonstration
using (gui.Node().Height(150).Direction(Axis.Vertical).Gap(8).Enter())
{
    gui.DrawText("Easing Functions Demo:", 16, Color.White);

    var time = (gui.Time.Elapsed % 2.0f) / 2.0f; // 2-second loop
    var easingFunctions = new[] {
        ("Linear", (Func<float, float>)Easing.Linear),
        ("EaseIn", Easing.EaseIn),
        ("EaseOut", Easing.EaseOut),
        ("SmoothStep", Easing.SmoothStep),
        ("BackOut", Easing.BackOut),
        ("ElasticOut", Easing.ElasticOut)
    };

    foreach (var (name, easingFunc) in easingFunctions)
    {
        using (gui.Node().Height(20).Direction(Axis.Horizontal).Gap(10).Enter())
        {
            using (gui.Node(80, 20).Enter())
            {
                gui.DrawText($"{name}:", 12, Color.Gray);
            }

            using (gui.Node(150, 15).Enter())
            {
                var easedValue = easingFunc(time);
                var fillWidth = gui.CurrentNode.Rect.W * easedValue;

                gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(60, 60, 60), 3);
                if (fillWidth > 0)
                {
                    var fillRect = new Rect(gui.CurrentNode.Rect.X, gui.CurrentNode.Rect.Y, fillWidth, gui.CurrentNode.Rect.H);
                    gui.DrawRect(fillRect, Color.FromArgb(74, 144, 226), 3);
                }
            }
        }
    }
}

// Animate a value smoothly
var animatedValue = gui.GetAnimationFloat(targetValue);
animatedValue.AnimateTo(targetValue, 0.3f, Easing.SmoothStep);
var currentValue = animatedValue.GetValue();

// Popup animation example
using (gui.Node(120, 35).Enter())
{
    var popupButtonInteractable = gui.GetInteractable();
    gui.DrawBackgroundRect(Color.FromArgb(80, 80, 80), 5);
    gui.DrawText("Toggle Popup", 12, Color.White);

    if (popupButtonInteractable.OnClick())
    {
        if (popupVisibility.GetValue() > 0.5f)
        {
            popupVisibility.AnimateTo(0, 0.3f, Easing.SmoothStep);
        }
        else
        {
            popupVisibility.AnimateTo(1, 0.3f, Easing.SmoothStep);
        }
    }
}
```

### Hover Animations

```csharp
// Animated popup overlay
if (popupVisibility.GetValue() > 0.001f)
{
    var alpha = popupVisibility.GetValue();
    var scale = Vector2.Lerp(new Vector2(0.8f), Vector2.One, alpha);

        using (gui.Node(100, 40).Enter())
        {
            var interactable = gui.GetInteractable();
            var hoverAmount = gui.AnimateBool01(interactable.OnHover(), 0.2f, Easing.SmoothStep);
            var bgColor = Color.FromArgb((int)(255 * (0.3f + hoverAmount * 0.3f)), 70, 130, 180);

            gui.DrawBackgroundRect(bgColor, 8);
            gui.DrawText("Hover me!", Color.White);
        }
```

                // Semi-transparent overlay
                gui.DrawRect(gui.ScreenRect, Color.FromArgb((int)(128 * alpha), 0, 0, 0));
## Shape

                // Popup content
                var popupSize = new Vector2(300, 200) * scale;
                var popupRect = new Rect(
                    (gui.ScreenRect.W - popupSize.X) / 2,
                    (gui.ScreenRect.H - popupSize.Y) / 2,
                    popupSize.X, popupSize.Y);
```csharp
using Guinevere;
using Guinevere.OpenGL.SilkNET;
using System.Numerics;

                gui.DrawRect(popupRect, Color.FromArgb((int)(255 * alpha), 45, 45, 65), 12);
public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui);

                // Popup text (positioned manually for this example)
                var textAlpha = (int)(255 * alpha);
                gui.DrawText("Animated Popup!", 20, Color.FromArgb(textAlpha, 255, 255, 255));
            }
        win.RunGui(() =>
        {
            var center = new Vector2(200, 200);

            // Basic shapes
            var circle = Shape.Circle(50);
            var rect = Shape.Rectangle(100, 60);
            var roundedRect = Shape.RectangleRounded(120, 80, 20);

            gui.DrawShape(center, circle).SolidColor(Color.Red);
            gui.DrawShape(center + new Vector2(150, 0), rect).SolidColor(Color.Blue);
            gui.DrawShape(center + new Vector2(0, 150), roundedRect).SolidColor(Color.Green);
        });
    }
}
```

### Shape Operations

```csharp
// Combine shapes
var combined = Shape.Circle(60) + Shape.Rectangle(80, 40);
var subtracted = Shape.Circle(60) - Shape.Circle(30);
var intersected = Shape.Circle(60) * Shape.Rectangle(80, 40);

gui.DrawShape(center, combined).SolidColor(Color.Purple);
```

### Shape Transformations

```csharp
var shape = Shape.Circle(40)
    .Expand(10)           // Make bigger
    .Rotate(Angle.Degrees(45))  // Rotate
    .Scale(1.5f, 0.8f)    // Scale X and Y
    .Move(20, -10);       // Move position

gui.DrawShape(center, shape).SolidColor(Color.Orange);
```

### Advanced Shape Effects

```csharp
gui.DrawShape(center, Shape.Circle(80))
    .LinearGradientColor(Color.Red, Color.Blue, Angle.Degrees(45))
    .InnerShadow(Color.Black, new Vector2(0, 5), 10, -5)
    .OuterShadow(Color.Gray, new Vector2(2, 2), 8);
```

## Basic Controls

```csharp
using Guinevere;
using Guinevere.OpenGL.SilkNET;

public abstract class Program
{
    // Only declare each variable once for clarity in code samples
    private static bool checkbox = false;
    private static bool toggle = true;
    private static string textInput = "Hello";
    private static string passwordInput = "";
    private static string textArea = "Multi-line\ntext area\nexample";
    private static int dropdown = 0;
    private static readonly string[] options = { "Option 1", "Option 2", "Option 3" };
    private static int activeTab = 0;

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1000, 800, "UI Controls Demo");
        using var win = new GuiWindow(gui);

        win.RunGui(() =>
        {
            gui.DrawRect(gui.ScreenRect, Color.FromArgb(245, 245, 245));

            using (gui.Node().Expand().Margin(20).Direction(Axis.Vertical).Gap(20).Enter())
            {
                gui.DrawBackgroundRect(Color.White, radius: 10);

                using (gui.Node().Expand().Margin(20).Direction(Axis.Horizontal).Gap(30).Enter())
                {
                    // Left side - Controls
                    using (gui.Node().Width(500).Direction(Axis.Vertical).Gap(20).Enter())
                    {
                        gui.DrawText("UI Controls Demo", 24, Color.FromArgb(51, 51, 51));

                        // Buttons
                        using (gui.Node().Height(80).Direction(Axis.Vertical).Gap(10).Enter())
                        {
                            gui.DrawText("Buttons:", 16, Color.FromArgb(51, 51, 51));

                            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                            {
                                if (gui.Button("Primary"))
                                    Console.WriteLine("Primary clicked!");

                                if (gui.Button("Success", backgroundColor: Color.FromArgb(76, 175, 80)))
                                    Console.WriteLine("Success clicked!");

                                if (gui.Button("Danger", backgroundColor: Color.FromArgb(244, 67, 54)))
                                    Console.WriteLine("Danger clicked!");

                                if (gui.IconButton("🔍", size: 40))
                                    Console.WriteLine("Search clicked!");
                            }
                        }

                        // Checkboxes and Toggles
                        using (gui.Node().Height(100).Direction(Axis.Vertical).Gap(10).Enter())
                        {
                            gui.DrawText("Checkboxes & Toggles:", 16, Color.FromArgb(51, 51, 51));

                            using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
                            {
                                gui.Checkbox(ref checkbox1, "Enable notifications");
                                gui.Checkbox(ref checkbox2, "Auto-save");
                            }

                            using (gui.Node().Height(30).Direction(Axis.Horizontal).Gap(20).Enter())
                            {
                                gui.Toggle(ref toggle1, "Dark mode");
                                gui.Toggle(ref toggle2, "High contrast",
                                    onColor: Color.FromArgb(156, 39, 176));
                            }
                        }

                        // Text Inputs
                        using (gui.Node().Height(140).Direction(Axis.Vertical).Gap(10).Enter())
                        {
                            gui.DrawText("Text Inputs:", 16, Color.FromArgb(51, 51, 51));

                            using (gui.Node().Height(35).Direction(Axis.Horizontal).Gap(10).Enter())
                            {
                                using (gui.Node().Width(200).Enter())
                                {
                                    textInput = gui.TextInput(textInput, placeholder: "Enter text...");
                                }

                                using (gui.Node().Width(200).Enter())
                                {
                                    passwordInput = gui.PasswordInput(passwordInput, placeholder: "Password");
                                }
                            }

                            using (gui.Node().Height(80).Enter())
                            {
                                textArea = gui.TextArea(textArea, width: 420, height: 80, placeholder: "Multiline text...");
                            }
                        }

                        // Dropdowns
                        using (gui.Node().Height(80).Direction(Axis.Vertical).Gap(10).Enter())
                        {
                            gui.DrawText("Dropdowns:", 16, Color.FromArgb(51, 51, 51));

                            using (gui.Node().Height(40).Direction(Axis.Horizontal).Gap(10).Enter())
                            {
                                using (gui.Node().Width(200).Enter())
                                {
                                    gui.Dropdown(dropdownOptions, ref dropdown1, placeholder: "Choose option...");
                                }

                                using (gui.Node().Width(200).Enter())
                                {
                                    gui.Dropdown(dropdownOptions, ref dropdown2, selectedColor: Color.FromArgb(76, 175, 80));
                                }
                            }
                        }

                        // Tabs
                        using (gui.Node().Height(120).Direction(Axis.Vertical).Gap(10).Enter())
                        {
                            gui.DrawText("Tabs:", 16, Color.FromArgb(51, 51, 51));

                            // Horizontal tabs
                            gui.Tabs(ref activeTab, tabs =>
                            {
                                tabs.Tab("Overview", () => gui.DrawText("Overview content", 12, Color.Gray));
                                tabs.Tab("Details", () => gui.DrawText("Details content", 12, Color.Gray));
                                tabs.Tab("Settings", () => gui.DrawText("Settings content", 12, Color.Gray));
                            });

                            // Pill tabs
                            gui.PillTabs(ref pillTab, tabs =>
                            {
                                tabs.Tab("Home");
                                tabs.Tab("Profile");
                                tabs.Tab("Messages");
                            }, activeTabColor: Color.FromArgb(76, 175, 80));
                        }

                        // Vertical tabs
                        using (gui.Node().Height(100).Direction(Axis.Horizontal).Enter())
                        {
                            gui.VerticalTabs(ref verticalTab, tabs =>
                            {
                                tabs.Tab("Account", () =>
                                    gui.DrawText("Account settings and profile information", 12, Color.Gray));
                                tabs.Tab("Security", () =>
                                    gui.DrawText("Password and authentication settings", 12, Color.Gray));
                                tabs.Tab("Notifications", () =>
                                    gui.DrawText("Email and push notification preferences", 12, Color.Gray));
                            });
                        }
                    }

                    // Right side - Current Values
                    using (gui.Node().Width(250).Direction(Axis.Vertical).Gap(8).Enter())
                    {
                        gui.DrawBackgroundRect(Color.FromArgb(248, 249, 250), radius: 8);

                        using (gui.Node().Expand().Margin(15).Direction(Axis.Vertical).Gap(8).Enter())
                        {
                            gui.DrawText("Current Values:", 16, Color.FromArgb(51, 51, 51));

                            gui.DrawText($"Checkbox 1: {checkbox1}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Checkbox 2: {checkbox2}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Toggle 1: {toggle1}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Toggle 2: {toggle2}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Text: \"{textInput}\"", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Password: {new string('*', passwordInput.Length)}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Text Area Lines: {textArea.Split('\n').Length}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Dropdown 1: {(dropdown1 >= 0 ? dropdownOptions[dropdown1] : "None")}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Dropdown 2: {(dropdown2 >= 0 ? dropdownOptions[dropdown2] : "None")}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Active Tab: {activeTab}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Pill Tab: {pillTab}", 12, Color.FromArgb(102, 102, 102));
                            gui.DrawText($"Vertical Tab: {verticalTab}", 12, Color.FromArgb(102, 102, 102));

                            gui.DrawText($"FPS: {gui.Time.SmoothFps:F1}", 12, Color.FromArgb(153, 153, 153));
                        }
                    }
                }
            }
            // Buttons
            if (gui.Button("Click me!"))
                Console.WriteLine("Button clicked!");

            if (gui.Button("Custom", backgroundColor: Color.Red, width: 120))
                Console.WriteLine("Custom button!");

            if (gui.IconButton("🔍", size: 40))
                Console.WriteLine("Search!");

            // Checkboxes and toggles
            gui.Checkbox(ref checkbox, "Enable feature");
            gui.Toggle(ref toggle, "Dark mode");

            // Text inputs
            textInput = gui.TextInput(textInput, placeholder: "Enter text...");

            // Dropdown
            gui.Dropdown(options, ref dropdown, placeholder: "Choose...");

            // Display values
            gui.DrawText($"Checkbox: {checkbox}, Toggle: {toggle}", 12, Color.Gray);
            gui.DrawText($"Input: '{textInput}', Selected: {(dropdown >= 0 ? options[dropdown] : "None")}", 12, Color.Gray);
        });
    }
}
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

## Advanced Features

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

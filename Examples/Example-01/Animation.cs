using Guinevere;
using GuinevereDemos;

namespace Example_01;

public partial class Shared
{
    static void Animation(Gui gui)
    {
        using (gui.Node().Expand().Gap(20).Enter())
        {
            DrawLayoutBlendingDemo(gui);
            DrawSliderAnimationDemo(gui);
            DrawEasingFunctionsDemo(gui);
        }
    }

    static void DrawLayoutBlendingDemo(Gui gui)
    {
        using (gui.Node().Expand().Padding(20).Gap(10).Enter())
        {
            gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(52, 58, 64), 8);
            gui.DrawText("Composable layout: 120px ↔ 70%", 16, Color.White);

            var progress = (MathF.Sin(gui.Time.Elapsed * 1.5f) + 1f) * 0.5f;
            var width = UnitValue.Lerp(UnitValue.Pixels(120f), UnitValue.Percentage(0.7f), progress);
            using (gui.Node().Width(width).Height(44).Enter())
            {
                gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(114, 92, 214), 6);
                gui.DrawText($"blend {progress:P0}", 12, Color.White);
            }
        }
    }

    static void DrawSliderAnimationDemo(Gui gui)
    {
        using (gui.Node().Expand().Enter())
        {
            gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(52, 58, 64), 8);

            for (var i = 0; i < 5; i++)
            {
                var phase = gui.Time.Elapsed + (i * Math.PI / 3);
                var x = (float)(Math.Sin(phase * 0.8) * 100);
                var y = (float)(Math.Cos(phase * 0.6) * 50);
                var scale = (float)(0.8 + 0.3 * Math.Sin(phase * 1.2));

                var size = 80 * scale;

                using (gui.Node(size, size)
                           .AlignSelf(0.5f + x / 400f)
                           .AlignContent(0.5f + y / 200f)
                           .Enter())
                {
                    var hue = (i * 60 + gui.Time.Elapsed * 30) % 360;
                    var color = HsvToColor(hue, 0.7f, 0.9f);
                    gui.DrawRect(gui.CurrentNode.Rect, color, size / 4);

                    gui.DrawText($"Panel {i + 1}", 12, Color.White);
                }
            }
        }
    }

    static Color HsvToColor(double hue, double saturation, double value)
    {
        // Simple HSV to RGB conversion
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        var m = value - c;

        double r, g, b;

        if (hue < 60)
        {
            r = c;
            g = x;
            b = 0;
        }
        else if (hue < 120)
        {
            r = x;
            g = c;
            b = 0;
        }
        else if (hue < 180)
        {
            r = 0;
            g = c;
            b = x;
        }
        else if (hue < 240)
        {
            r = 0;
            g = x;
            b = c;
        }
        else if (hue < 300)
        {
            r = x;
            g = 0;
            b = c;
        }
        else
        {
            r = c;
            g = 0;
            b = x;
        }

        return Color.FromArgb(255,
            (int)((r + m) * 255),
            (int)((g + m) * 255),
            (int)((b + m) * 255));
    }

    static void DrawEasingFunctionsDemo(Gui gui)
    {
        using (gui.Node().Expand().Enter())
        {
            gui.DrawRect(gui.CurrentNode.Rect, Color.FromArgb(52, 58, 64), 8);

            using (gui.Node().Expand().Margin(20).Gap(10).Enter())
            {
                gui.DrawText("Easing Functions Comparison", 18, Color.White);

                var time = (gui.Time.Elapsed % 3.0f) / 3.0f; // 3-second loop

                var easingFunctions = new[]
                {
                    ("Linear", (Func<float, float>)Easing.Linear), ("EaseIn", Easing.EaseIn),
                    ("EaseOut", Easing.EaseOut), ("SmoothStep", Easing.SmoothStep), ("BackOut", Easing.BackOut),
                    ("ElasticOut", Easing.ElasticOut)
                };

                using (gui.Node().Expand().Gap(8).Enter())
                {
                    foreach (var (name, easingFunc) in easingFunctions)
                    {
                        using (gui.Node().Height(25).Direction(Axis.Horizontal).Gap(15).Enter())
                        {
                            // Label
                            using (gui.Node(80, 25).AlignContent(0.0f).Enter())
                            {
                                gui.DrawText($"{name}:", 12, Color.FromArgb(200, 200, 200));
                            }

                            // Progress bar
                            using (gui.Node(200, 15).AlignSelf(0.5f).Enter())
                            {
                                var barRect = gui.CurrentNode.Rect;
                                var easedValue = easingFunc(time);
                                var fillWidth = barRect.W * easedValue;

                                // Background
                                gui.DrawRect(barRect, Color.FromArgb(74, 85, 104), 3);

                                // Fill
                                if (fillWidth > 0)
                                {
                                    var fillRect = new Rect(barRect.X, barRect.Y, fillWidth, barRect.H);
                                    gui.DrawRect(fillRect, Color.FromArgb(74, 144, 226), 3);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

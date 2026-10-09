using Guinevere;
using GuinevereDemos;

namespace Example_06_Animation;

abstract class Program
{
    static Gui _gui = null!;

    public static void Main()
    {
        _gui = new Gui();
        using var win = new GuiWindow(_gui, 1200, 800, "Animation System Demo");

        win.RunGui(RenderGui);
    }

    static void RenderGui()
    {
        DemoHeader.Header(_gui, "Guinevere - Animation");
        _gui.DrawRect(_gui.ScreenRect, Color.FromArgb(32, 32, 48));

        using (_gui.Node().Expand().Gap(20).Enter())
        {
            using (_gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
            {
                DrawLayoutBlendingDemo();
                MotionDemos.Springs(_gui);
            }
            using (_gui.Node().Expand().Direction(Axis.Horizontal).Gap(20).Enter())
            {
                MotionDemos.Transforms(_gui);
                DrawEasingFunctionsDemo();
            }
        }
    }

    static void DrawLayoutBlendingDemo()
    {
        using (_gui.Node().Expand().Padding(20).Gap(10).Enter())
        {
            _gui.DrawRect(_gui.CurrentNode.Rect, Color.FromArgb(52, 58, 64), 8);
            _gui.DrawText("Composable layout: 120px ↔ 70%", 16, Color.White);

            _gui.RequestFrame();
            var progress = (MathF.Sin(_gui.Time.Elapsed * 1.5f) + 1f) * 0.5f;
            var width = UnitValue.Lerp(UnitValue.Pixels(120f), UnitValue.Percentage(0.7f), progress);
            using (_gui.Node().Width(width).Height(44).Enter())
            {
                _gui.DrawRect(_gui.CurrentNode.Rect, Color.FromArgb(114, 92, 214), 6);
                _gui.DrawText($"blend {progress:P0}", 12, Color.White);
            }
        }
    }

    internal static Color HsvToColor(double hue, double saturation, double value)
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

    static void DrawEasingFunctionsDemo()
    {
        using (_gui.Node().Expand().Enter())
        {
            _gui.DrawRect(_gui.CurrentNode.Rect, Color.FromArgb(52, 58, 64), 8);

            using (_gui.Node().Expand().Margin(20).Gap(10).Enter())
            {
                _gui.DrawText("Easing Functions Comparison", 18, Color.White);

                _gui.RequestFrame();
                var time = (_gui.Time.Elapsed % 3.0f) / 3.0f; // 3-second loop

                Easing.TryParse("ease", out var cssEase);
                var easingFunctions = new[]
                {
                    ("Linear", (Func<float, float>)Easing.Linear), ("EaseInOutSine", Easing.EaseInOutSine),
                    ("CSS ease", cssEase), ("SmoothStep", Easing.SmoothStep), ("EaseOutBack", Easing.EaseOutBack),
                    ("EaseOutElastic", Easing.EaseOutElastic)
                };

                using (_gui.Node().Expand().Gap(8).Enter())
                {
                    foreach (var (name, easingFunc) in easingFunctions)
                    {
                        using (_gui.Node().Height(25).Direction(Axis.Horizontal).Gap(15).Enter())
                        {
                            // Label
                            using (_gui.Node(110, 25).AlignContent(0.0f).Enter())
                            {
                                _gui.DrawText($"{name}:", 12, Color.FromArgb(200, 200, 200));
                            }

                            // Progress bar
                            using (_gui.Node(200, 15).AlignSelf(0.5f).Enter())
                            {
                                var barRect = _gui.CurrentNode.Rect;
                                var easedValue = easingFunc(time);
                                var fillWidth = barRect.W * easedValue;

                                // Background
                                _gui.DrawRect(barRect, Color.FromArgb(74, 85, 104), 3);

                                // Fill
                                if (fillWidth > 0)
                                {
                                    var fillRect = new Rect(barRect.X, barRect.Y, fillWidth, barRect.H);
                                    _gui.DrawRect(fillRect, Color.FromArgb(74, 144, 226), 3);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

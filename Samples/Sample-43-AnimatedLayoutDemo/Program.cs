using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_43_AnimatedLayoutDemo;

public abstract class Program
{
    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1024, 768, "Animated Layout Demo");

        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        var time = gui.Time.Elapsed;

        // Animated background
        var bgColor = Color.FromArgb(
            255,
            (int)(128 + 127 * Math.Sin(time * 0.5)),
            (int)(128 + 127 * Math.Sin(time * 0.3)),
            (int)(128 + 127 * Math.Sin(time * 0.7))
        );
        gui.DrawRect(gui.ScreenRect, bgColor);

        // Floating panels with physics-like movement
        using (gui.Node().Expand().Margin(50).Enter())
        {
            for (var i = 0; i < 5; i++)
            {
                var phase = time + (i * Math.PI / 3);
                var x = (float)(Math.Sin(phase * 0.8) * 100);
                var y = (float)(Math.Cos(phase * 0.6) * 50);
                var scale = (float)(0.8 + 0.3 * Math.Sin(phase * 1.2));

                var size = 80 * scale;

                using (gui.Node(size, size)
                           .AlignSelf(0.5f + x / 400f)
                           .AlignContent(0.5f + y / 200f)
                           .Enter())
                {
                    var hue = (i * 60 + time * 30) % 360;
                    var color = HsvToColor(hue, 0.7f, 0.9f);
                    gui.DrawRect(gui.CurrentNode.Rect, color, size / 4);

                    gui.DrawText($"Panel {i + 1}", 12, Color.White);
                }
            }
        }

        // Rotating menu
        using (gui.Node(200, 200).AlignSelf(0.8f).AlignContent(0.2f).Enter())
        {
            for (var i = 0; i < 6; i++)
            {
                // Create a small fixed position node for each menu item
                var itemColor = HsvToColor(i * 60, 0.8f, 0.9f);
                // Note: This would need enhanced positioning support in the actual implementation
                gui.DrawText($"Item {i + 1}", 10, itemColor);
            }
        }
    }

    private static Color HsvToColor(double hue, double saturation, double value)
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
}

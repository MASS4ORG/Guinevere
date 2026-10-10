using System.Numerics;
using Guinevere;

namespace Example_01;

public partial class Shared
{
    const string TextShort = "Guinevere GUI çãóé⚙️☀️▶️❤️😀";

    const string TextLong =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.";

    static readonly string[] ColorNames = ["Text", "Accent", "Positive", "Warning", "Negative"];
    static int typeSize = 32;
    static int typeColor;

    static float wrapWidth = 360f;
    static int effectSize = 24;

    /// <summary>A live specimen: change the size and color, and see the same string next to a type ramp.</summary>
    static void Typography(Gui gui, ColorPalette p)
    {
        var colors = new[] { p.Text, p.Accent, p.Positive, p.Warning, p.Negative };
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(14).Enter())
        {
            gui.ScrollY(p.Track, p.Border);
            using (gui.Node().Direction(Axis.Horizontal).Gap(24).ContentAlignY(0.5f).Enter())
            {
                Stepper(gui, p, "type-size", "Size", typeSize, 10, 72, value => typeSize = value, step: 2);
                Choice(gui, p, "type-color", ColorNames, typeColor, index => typeColor = index);
            }

            using (gui.Node().ExpandWidth().Padding(20).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                gui.DrawText(TextShort, typeSize, colors[typeColor]);
            }

            Label(gui, p, "Type ramp");
            using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(18).ContentAlignY(1f).Enter())
                foreach (var size in new[] { 11, 13, 16, 20, 28, 40 })
                    using (gui.Node().Direction(Axis.Vertical).ContentAlignX(0.5f).Enter())
                    {
                        gui.DrawText("Aa", size, p.Text);
                        Label(gui, p, $"{size}");
                    }

            Label(gui, p, "Fallback and multi-line");
            using (gui.Node().ExpandWidth().Direction(Axis.Vertical).Gap(4).Padding(14).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                gui.DrawText("Unicode: 🚩❓💣★ ♥ ♦ ♣ ♠ → ← ↑ ↓", 15, p.Text);
                gui.DrawText("Non ASCII: çáããüaáéíóú! 🌟", 15, p.Text);
                gui.DrawText("Symbols: !@#$%^&*()_+-=[]{}|;':\",./<>?", 15, p.TextDim);
                gui.DrawText("One call,\nthree lines,\ncentered together.", 15, p.Series[1]);
            }
        }
    }

    /// <summary>Drag the handle to change wrapWidth; both blocks reflow with their own size and color.</summary>
    static void Wrapping(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            Label(gui, p, $"wrapWidth: {wrapWidth:0}px");
            using (gui.Node().Expand().Direction(Axis.Horizontal).Enter())
            {
                using (gui.Node().Width(wrapWidth + 32).ExpandHeight().Direction(Axis.Vertical).Padding(16).Gap(14)
                           .Enter())
                {
                    gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                    gui.ScrollY(p.Track, p.Border);
                    gui.DrawText(TextLong, 14, p.Text, wrapWidth: wrapWidth, centerInRect: false);
                    gui.DrawText(TextLong, 18, p.Accent, wrapWidth: wrapWidth, centerInRect: false);
                    gui.DrawText(TextLong, 12, p.TextDim, wrapWidth: wrapWidth, centerInRect: false);
                }

                ResizeHandle(gui, p, "wrap/handle", () => wrapWidth, value => wrapWidth = value, 120f, 800f);
            }
        }
    }

    static void Effects(Gui gui, ColorPalette p)
    {
        var outline = p.Background;
        var shadow = new Color(Color.Black, 0.75f);
        var from = p.Pick(0);
        var to = p.Pick(3);
        var angle = gui.Time.Elapsed * 60f % 360f;
        gui.RequestFrame();

        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(10).Enter())
        {
            Stepper(gui, p, "effect-size", "Size", effectSize, 20, 64, value => effectSize = value, step: 4);
            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(8).Enter())
            {
                gui.ScrollY(p.Track, p.Border);
                EffectRow(gui, p, "flat", null);
                EffectRow(gui, p, "outline", new TextEffects { Outline = new TextEffects.TextOutline(outline, 2.5f) });
                EffectRow(gui, p, "drop shadow",
                    new TextEffects { DropShadow = new TextEffects.TextShadow(shadow, new Vector2(3, 5), 6f) });
                EffectRow(gui, p, "inner shadow", new TextEffects
                {
                    Gradient = new TextEffects.TextGradient(from, from),
                    InnerShadow = new TextEffects.TextShadow(new Color(Color.Black, 0.85f), new Vector2(0, 4), 3f),
                });
                EffectRow(gui, p, "linear gradient", new TextEffects { Gradient = new TextEffects.TextGradient(from, to, 25f) });
                EffectRow(gui, p, "radial gradient",
                    new TextEffects { Gradient = new TextEffects.TextGradient(from, to, Radial: true) });
                EffectRow(gui, p, "animated angle",
                    new TextEffects { Gradient = new TextEffects.TextGradient(from, to, angle) });
                EffectRow(gui, p, "combined", new TextEffects
                {
                    Outline = new TextEffects.TextOutline(outline, 2.5f),
                    DropShadow = new TextEffects.TextShadow(shadow, new Vector2(3, 5), 6f),
                    Gradient = new TextEffects.TextGradient(from, to, 25f),
                });
            }
        }
    }

    static void EffectRow(Gui gui, ColorPalette p, string label, TextEffects? effects)
    {
        using var key = gui.ItemKey(label);
        using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Gap(24).Padding(14, 8).ContentAlignY(0.5f).Enter())
        {
            gui.DrawBackgroundRect(p.SurfaceRaised, 10);
            using (gui.Node(width: 130).ContentAlignY(0.5f).Enter()) Label(gui, p, label);
            gui.DrawText(TextShort, effectSize, p.Text, effects: effects);
        }
    }

    /// <summary>Two independently scrolling columns: a color sweep and long, mixed-size lines.</summary>
    static void Scrolling(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(16).Enter())
        {
            using (gui.Node().Expand().Direction(Axis.Vertical).Padding(14).Gap(2).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                gui.ScrollY(p.Track, p.Border);
                for (var i = 0; i < 36; i++)
                {
                    var hue = i * 10 * Math.PI / 180;
                    var color = Color.FromArgb(255,
                        (int)(Math.Sin(hue) * 127 + 128),
                        (int)(Math.Sin(hue + 2.094) * 127 + 128),
                        (int)(Math.Sin(hue + 4.189) * 127 + 128));
                    gui.DrawText($"Color sweep {i + 1}: the quick brown fox", 14, color);
                }
            }

            using (gui.Node().Expand().Direction(Axis.Vertical).Padding(14).Gap(2).Enter())
            {
                gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                gui.ScrollY(p.Track, p.Border);
                for (var i = 0; i < 40; i++)
                    gui.DrawText($"Line {i + 1}: lorem ipsum dolor sit amet", 10 + i % 4 * 2,
                        i % 5 == 0 ? p.Accent : p.Text);
            }
        }
    }
}

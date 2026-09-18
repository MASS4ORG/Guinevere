using System.Numerics;
using System.Text;
using Guinevere;
using GuinevereDemos;

namespace Example;

/// <summary>
/// Text rendering showcase: plain and styled text, wrapping, colors, <see cref="TextEffects"/>
/// (outline, drop shadows, inner shadows, gradients), organized in scrollable tabs.
/// </summary>
public abstract class Program
{
    static int _activeTabIndex;

    const string TextShort = "Guinevere GUI çãóé⚙️☀️▶️❤️😀";

    const string TextLong =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

    static readonly Color Ink = Color.FromArgb(255, 245, 247, 250);
    static readonly Color RowBg = Color.FromArgb(255, 40, 44, 56);
    static readonly Color OutlineInk = Color.FromArgb(255, 16, 20, 30);
    static readonly Color ShadowInk = Color.FromArgb(190, 0, 0, 0);
    static readonly Color GradA = Color.FromArgb(255, 70, 130, 245);
    static readonly Color GradB = Color.FromArgb(255, 235, 80, 130);

    public static void Main()
    {
        var gui = new Gui();
        using var win = new GuiWindow(gui, 1000, 850, "Text & Text Effects");

        win.RunGui(() => Draw(gui));
    }

    static void Draw(Gui gui)
    {
        gui.DrawWindowTitlebar();
        DemoHeader.Header(gui, "Guinevere - Texts");

        using (gui.Node().Expand().Enter())
        {
            gui.Tabs(ref _activeTabIndex, tabs =>
            {
                tabs.Tab("Basic", () => BasicText(gui));
                tabs.Tab("Wrapping", () => Wrapping(gui));
                tabs.Tab("Color", () => ColorDemo(gui));
                tabs.Tab("Effects", () => TextEffects(gui));
                tabs.Tab("Long Content", () => LongContent(gui));
            });
        }
    }

    static void BasicText(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
        {
            gui.ScrollY(Color.Black, Color.Gray);

            gui.DrawText("Single line text", 14);
            gui.DrawText("Multi-line text:\nLine 1\nLine 2\nLine 3", 14);
            gui.DrawText("Colored text", 14, Color.Red);
            gui.DrawText("Large text", 20);
            gui.DrawText("Small text", 10);

            gui.Node(10, 20); // Spacer

            gui.DrawText("Special Characters & Formatting:", 18);
            gui.DrawText("Unicode: 🚩❓💣★ ♥ ♦ ♣ ♠ → ← ↑ ↓", 14);
            gui.DrawText("Numbers: 0123456789", 14);
            gui.DrawText("Symbols: !@#$%^&*()_+-=[]{}|;':\",./<>?", 14);
            gui.DrawText("Non ASCII: çáããüaáéíóú! 🌟", 14);

            gui.Node(10, 20); // Spacer

            gui.DrawText("Multi-line alignment:", 18);
            gui.DrawText("Line 1 - left aligned text runs from the node's start.", 12);
            gui.DrawText("Line 2 - each line of a single DrawText call is centered together.", 12);
            gui.DrawText("Line 3 - use separate calls to keep the text left aligned.", 12);
        }
    }

    static void Wrapping(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
        {
            gui.ScrollY(Color.Black, Color.Gray);
            gui.DrawText("Text Wrapping Examples:", 18);

            gui.Node(10, 10); // Spacer

            gui.DrawText("Unwrapped long text:", 14);
            gui.DrawText(TextLong, 12);

            gui.Node(10, 10); // Spacer

            gui.DrawText("Wrapped text (600 px width):", 14);
            gui.DrawText(TextLong, 12, wrapWidth: 600);

            gui.Node(10, 10); // Spacer

            gui.DrawText("Wrapped text (300 px width):", 14);
            gui.DrawText(TextLong, 12, wrapWidth: 300);

            gui.Node(10, 20); // Spacer

            gui.DrawText("Mixed sizes & colors:", 16);
            gui.DrawText(TextLong, 12, Color.DarkGreen);
            gui.DrawText(TextLong, 13, Color.FromArgb(255, 100, 100, 240), wrapWidth: 450);

            gui.Node(10, 20); // Spacer

            for (var i = 0; i < 10; i++)
            {
                gui.DrawText($"Extra text line {i + 1}: The quick brown fox jumps over the lazy dog", 12);
            }
        }
    }

    static void ColorDemo(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
        {
            gui.ScrollY(Color.Black, Color.Gray);

            for (var i = 0; i < 25; i++)
            {
                var hue = (i * 15) % 360;
                var color = Color.FromArgb(255,
                    (int)(Math.Sin(hue * Math.PI / 180) * 127 + 128),
                    (int)(Math.Sin((hue + 120) * Math.PI / 180) * 127 + 128),
                    (int)(Math.Sin((hue + 240) * Math.PI / 180) * 127 + 128));
                gui.DrawText(
                    $"Rainbow text line {i + 1} - The quick brown fox jumps over the lazy dog", 14, color);
            }
        }
    }

    static void TextEffects(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Gap(12).Enter())
        {
            gui.ScrollY(Color.Black, Color.Gray);

            EffectRow(gui, "flat", null);
            EffectRow(gui, "outline", new TextEffects { Outline = new TextEffects.TextOutline(OutlineInk, 2.5f), });
            EffectRow(gui, "drop shadow",
                new TextEffects { DropShadow = new TextEffects.TextShadow(ShadowInk, new Vector2(3, 5), 6f), });
            EffectRow(gui, "inner shadow",
                new TextEffects
                {
                    Gradient = new TextEffects.TextGradient(GradA, GradA),
                    InnerShadow = new TextEffects.TextShadow(Color.FromArgb(220, 0, 0, 0), new Vector2(0, 4), 3f),
                });
            EffectRow(gui, "linear gradient",
                new TextEffects { Gradient = new TextEffects.TextGradient(GradA, GradB, 25f), });
            EffectRow(gui, "radial gradient",
                new TextEffects { Gradient = new TextEffects.TextGradient(GradA, GradB, Radial: true), });
            EffectRow(gui, "combined",
                new TextEffects
                {
                    Outline = new TextEffects.TextOutline(OutlineInk, 2.5f),
                    DropShadow = new TextEffects.TextShadow(ShadowInk, new Vector2(3, 5), 6f),
                    Gradient = new TextEffects.TextGradient(GradA, GradB, 25f),
                });
        }
    }

    static void EffectRow(Gui gui, string label, TextEffects? effects)
    {
        using (gui.Node(height: 68).ExpandWidth().Direction(Axis.Horizontal).Gap(24).Padding(12, 14)
                   .AlignContent(0f, 0.5f).Enter())
        {
            gui.DrawBackgroundRect(RowBg, radius: 8);

            using (gui.Node(width: 160).Enter())
                gui.DrawText(label, 15, Color.FromArgb(255, 150, 156, 170));

            gui.DrawText(TextShort, 40, Ink, effects: effects);
        }
    }

    static void LongContent(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Padding(15).Enter())
        {
            gui.ScrollY(Color.Black, Color.Gray);
            gui.DrawText("Long Content (Scroll to see more):", 18);

            gui.Node(10, 15); // Spacer

            StringBuilder text = new();
            for (var i = 0; i < 40; i++)
            {
                text.AppendLine(
                    $"Line {i + 1}: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.");
            }

            gui.DrawText(text.ToString(), 12, Color.DarkSlateGray);

            gui.Node(10, 20); // Spacer

            gui.DrawText("Mixed Size:", 16);

            for (var i = 0; i < 30; i++)
            {
                var fontSize = 10 + (i % 4) * 2;
                var color = Color.FromArgb(255,
                    100 + (i * 15) % 155,
                    150,
                    200 - (i * 10) % 100);
                gui.Node(10, 20);
                {
                    gui.DrawText(
                        $"Mixed line {i + 1}: Various text sizes and colors to fill the scrollable area - this demonstrates the scrolling functionality",
                        fontSize, color);
                }
            }
        }
    }
}

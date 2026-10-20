using Guinevere;

namespace Example01;

public partial class Shared
{
    static readonly (string Name, RenderEffectStack Stack)[] FilterCards =
    [
        ("Blur", RenderEffectParser.Parse("blur(2)")),
        ("Glow", RenderEffectParser.Parse("glow(4,#ffbb44)")),
        ("Drop shadow", RenderEffectParser.Parse("drop-shadow(6,6,3,#3366ff)")),
        ("Saturation", RenderEffectParser.Parse("saturate(2)")),
        ("Brightness", RenderEffectParser.Parse("brightness(1.5)")),
        ("Contrast", RenderEffectParser.Parse("contrast(1.5)")),
        ("Tint", RenderEffectParser.Parse("tint(#33ccff,.5)")),
        ("Hue rotation", RenderEffectParser.Parse("hue-rotate(90deg)")),
        ("Grayscale", RenderEffectParser.Parse("grayscale(1)")),
        ("Inversion", RenderEffectParser.Parse("invert(1)")),
        ("Pixelation", RenderEffectParser.Parse("pixelate(5)")),
        ("Chromatic aberration", RenderEffectParser.Parse("chromatic-aberration(3,0)")),
        ("Sharpening", RenderEffectParser.Parse("sharpen(1)")),
    ];

    /// <summary>Shows each built-in filter on a panel containing text, shapes and an image.</summary>
    internal static void RenderEffectsDemo(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
        {
            gui.ScrollY(p.Track, p.Border);
            using (gui.Node().ExpandWidth().Direction(Axis.Horizontal).Wrap(1).Gap(16).Enter())
            {
                foreach (var (name, stack) in FilterCards)
                {
                    using var key = gui.ItemKey(name);
                    using (gui.Node(250, 170).Direction(Axis.Vertical).Gap(12).Enter())
                    {
                        gui.DrawText(name, 14, p.Text);
                        using (gui.Node(230, 125).Padding(12).Gap(8).Effects(stack).Enter())
                        {
                            gui.DrawBackgroundRect(p.SurfaceRaised, 12);
                            gui.DrawText("Guinevere", 22, p.Text);
                            using (gui.Node().Direction(Axis.Horizontal).Gap(12).Enter())
                            {
                                gui.Image(_richTextBadge ??= Badge(), 48, 48);
                                using (gui.Node(48, 48).Enter())
                                {
                                    gui.DrawBackgroundRect(p.Accent, 12);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

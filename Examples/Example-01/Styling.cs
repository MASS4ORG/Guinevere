using Guinevere;

namespace Example_01;

public partial class Shared
{
    static readonly StyleSheetSource stylesheet = StyleSheetSource.FromProvider(Theme);
    static readonly string[] StyleChips = ["Layout", "Text", "Motion", "Style"];
    const float SourcePaneMinWidth = 1050f;
    static bool styleSelected = true;
    static int styleChip = 3;
    static float styleProgress = 0.65f;

    /// <summary>The stylesheet follows the active palette; the Theme switch reloads it atomically.</summary>
    static string Theme()
    {
        var p = palette;
        var hover = ColorPalette.Hex(Color.Lerp(p.SurfaceHover, p.Accent, 0.3f));
        return $$"""
            @const radius = 12;
            $accent = {{ColorPalette.Hex(p.Accent)}};

            screen {
                width = expand;
                height = expand;
                gap = 16;

                toolbar {
                    flow-dir = x;
                    gap = 10;

                    > button { min-width = 120; }
                }
            }

            card {
                padding = 20;
                gap = 14;
                border-radius = @radius;
                bg-color = {{ColorPalette.Hex(p.SurfaceRaised)}};
                border-color = {{ColorPalette.Hex(p.Border)}};
                border-width = 1;
            }

            feature-card #inherit(card) {
                width = expand;
                box-shadow = 0 2 6 #00000040;

                :hover(0.3 ease-out) { translate = 0 -4; box-shadow = 0 14 28 #00000070; }
            }

            button {
                padding = 10 16;
                border-radius = 8;
                bg-color = {{ColorPalette.Hex(p.SurfaceHover)}};
                border-color = {{ColorPalette.Hex(p.Border)}};
                border-width = 1;
                scale = 1;

                :hover(0.15 ease-out) { bg-color = {{hover}}; scale = 1.05; }
                :active(0.08 ease-out) { scale = 0.96; }
                :selected(0.25 ease-in-out-sine) { bg-color = $accent; border-color = $accent; }
            }

            chip {
                padding = 6 14;
                border-radius = 16;
                border-width = 1;
                border-color = {{ColorPalette.Hex(p.Border)}};
                bg-color = {{ColorPalette.Hex(p.Surface)}};

                :hover(0.15 ease-out) { border-color = $accent; }
                :selected(0.2 ease-out) { bg-color = $accent; border-color = $accent; }
            }

            progress-track {
                width = expand;
                height = 24;
                border-radius = 12;
                bg-color = {{ColorPalette.Hex(p.Track)}};

                > progress-fill {
                    width = $progress;
                    height = expand;
                    border-radius = 12;
                    bg-color = $accent;
                }
            }
            """;
    }

    /// <summary>A styled column on the left; the stylesheet that produces it on the right.</summary>
    static void Styling(Gui gui, ColorPalette p)
    {
        using (gui.Node().Expand().Direction(Axis.Horizontal).Gap(16).Enter())
        {
            StyledColumn(gui, p);
            if (gui.ScreenRect.W >= SourcePaneMinWidth) SourcePane(gui, p);
        }
    }

    static void StyledColumn(Gui gui, ColorPalette p)
    {
        using (gui.StyledNode("screen").Enter())
        {
            gui.ScrollY(p.Track, p.Border);
            using (gui.StyledNode("toolbar").Enter())
            {
                StyledButton(gui, p, styleSelected ? "Selected" : "Select me", () => styleSelected = !styleSelected,
                    styleSelected);
                StyledButton(gui, p, "Reset", () =>
                {
                    styleSelected = true;
                    styleProgress = 0.65f;
                    styleChip = 3;
                });
            }

            using (gui.StyledNode("feature-card").Enter())
            {
                gui.DrawText("Inherited card: hover to lift", 20, p.Text);
                gui.DrawText("feature-card #inherit(card) receives the card layout and its selectors.", 13, p.TextDim);

                using (gui.StyledNode("progress-track").Enter())
                using (gui.StyledNode("progress-fill",
                           variables: [new StyleVariable("progress", $"{styleProgress * 100}%")]).Enter()) { }

                using (gui.StyledNode("toolbar").Enter())
                {
                    StyledButton(gui, p, "Less", () => styleProgress = Math.Max(0.1f, styleProgress - 0.1f));
                    StyledButton(gui, p, "More", () => styleProgress = Math.Min(1f, styleProgress + 0.1f));
                }
            }

            using (gui.StyledNode("card").Enter())
            {
                gui.DrawText("Modifiers: one :selected chip at a time", 16, p.Text);
                using (gui.Node().Direction(Axis.Horizontal).Gap(8).Wrap(1).Enter())
                    for (var i = 0; i < StyleChips.Length; i++)
                    {
                        using var key = gui.ItemKey(i);
                        var index = i;
                        var chip = gui.StyledNode("chip", i == styleChip ? ["selected"] : []);
                        if (chip.OnClick()) Later(() => styleChip = index);
                        using (chip.Enter())
                            gui.DrawText(StyleChips[i], 14, i == styleChip ? p.AccentText : p.Text);
                    }
            }
        }
    }

    static void SourcePane(Gui gui, ColorPalette p)
    {
        using (gui.Node(360).ExpandHeight().Direction(Axis.Vertical).Padding(14).Gap(2).Enter())
        {
            gui.DrawBackgroundRect(p.Track, 12);
            gui.ScrollY(p.Track, p.Border);
            gui.DrawText("Stylesheet source", 12, p.TextDim);
            foreach (var line in Theme().Split('\n'))
                gui.DrawText(line.Length == 0 ? " " : line, 11, p.TextDim);
        }
    }

    static void StyledButton(Gui gui, ColorPalette p, string label, Action clicked, bool selected = false)
    {
        var node = gui.StyledNode("button", selected ? ["selected"] : []);
        if (node.OnClick()) Later(clicked);
        using (node.Enter()) gui.DrawText(label, 15, selected ? p.AccentText : p.Text);
    }
}

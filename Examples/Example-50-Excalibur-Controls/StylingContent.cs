using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    const string Style = """
                         // tokens
                         $bg        = #12151d;
                         $card      = #202634;
                         $btn       = #ff0000;
                         $btn-hover = #00ff00;
                         $btn-down  = #0000ff;
                         $border    = #556080;

                         #root {
                             flex-direction = column;
                             gap = 16;
                             padding = 32;
                             background-color = $bg;
                         }

                         .card {
                             flex-direction = column;
                             gap = 12;
                             padding = 18;
                             background-color = $card;
                             border-radius = 10;
                         }
                         .row { flex-direction = row; gap = 12; }

                         .btn {
                             width = 150;
                             height = 46;
                             background-color = $btn;
                             border-radius = 8;
                             border-color = $border;
                             border-width = 1;
                             align-items = center;
                             justify-content = center;
                         }
                         .btn:hover  { background-color = $btn-hover; border-color = $btn-hover; }
                         .btn:active { background-color = $btn-down; }

                         #primary { background-color = $btn-hover; border-width = 0; }

                         .visuals { color = #eceef3; font-size = 14; }
                         .tile {
                             width = 120;
                             height = 64;
                             align-items = center;
                             justify-content = center;
                             background-color = #2c3446;
                             border-radius = 8;
                             cursor = pointer;
                         }
                         .tile:hover   { outline = 2px solid #88c0d0; outline-offset = 2; }
                         #corners      { border-radius = 18 0 18 0; }
                         #gradient     { background = linear-gradient(135deg, #5e81ac, #b48ead); }
                         #shadow       { box-shadow = 0 6px 14px #000000aa, inset 0 1px 0 #ffffff33; }
                         #faded        { opacity = 0.5; }
                         #bold         { font-weight = bold; font-style = italic; color = #ebcb8b; font-size = 16; }
                         """;

    static void StylingContent(Gui gui)
    {
        using (gui.StyledNode("VisualElement", id: "root").Expand().Enter())
        {
            gui.DrawText("PSS-styled widgets", 24, Color.FromArgb(255, 236, 238, 243));

            using (gui.StyledNode("VisualElement", ["card"]).Enter())
            {
                gui.DrawText(
                    "A .card panel — padding, radius and background come from the stylesheet. Hover a .btn to see the :hover rule.",
                    14, Color.FromArgb(255, 154, 160, 166));

                using (gui.StyledNode("VisualElement", ["row"]).Enter())
                {
                    StyledBtn(gui, "Normal", null);
                    StyledBtn(gui, "Primary", "primary");
                    StyledBtn(gui, "Also .btn", null);
                }

                using (gui.StyledNode("VisualElement", ["card", "visuals"]).Enter())
                {
                    gui.DrawText("Visual properties — text color and size are inherited from .visuals; hover a tile for its outline.");
                    using (gui.StyledNode("VisualElement", ["row"]).Enter())
                    {
                        StyledTile(gui, "corners", "Per-corner radius");
                        StyledTile(gui, "gradient", "Gradient");
                        StyledTile(gui, "shadow", "Shadows");
                        StyledTile(gui, "faded", "Opacity 50%");
                        StyledTile(gui, "bold", "Bold italic");
                    }
                }

                gui.DrawText(Style, color: Color.White, centerInRect: false);
            }
        }
    }

    static void StyledTile(Gui gui, string id, string label)
    {
        using (gui.StyledNode("VisualElement", ["tile"], id).Enter()) gui.DrawText(label);
    }

    static void StyledBtn(Gui gui, string label, string? id)
    {
        using (gui.StyledNode("Button", ["btn"], id).Enter())
        {
            _ = gui.GetInteractable().OnClick();
            gui.DrawText(label, 15, Color.FromArgb(255, 236, 238, 243));
        }
    }
}

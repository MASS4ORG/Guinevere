using Guinevere;

namespace Controls_01;

public abstract partial class Program
{
    const string Style = """
                                 /* variables */
                                 --bg:        #12151d;
                                 --card:      #202634;
                                 --btn:       #ff0000;
                                 --btn-hover: #00ff00;
                                 --btn-down:  #0000ff;
                                 --border:    #556080;

                                 #root {
                                     flex-direction: column;
                                     gap: 16;
                                     padding: 32;
                                     background-color: var(--bg);
                                 }

                                 .card {
                                     flex-direction: column;
                                     gap: 12;
                                     padding: 18;
                                     background-color: var(--card);
                                     border-radius: 10;
                                 }
                                 .row { flex-direction: row; gap: 12; }

                                 .btn {
                                     width: 150;
                                     height: 46;
                                     background-color: var(--btn);
                                     border-radius: 8;
                                     border-color: var(--border);
                                     border-width: 1;
                                     align-items: center;
                                     justify-content: center;
                                 }
                                 .btn:hover  { background-color: var(--btn-hover); border-color: var(--btn-hover); }
                                 .btn:active { background-color: var(--btn-down); }

                                 #primary { background-color: var(--btn-hover); border-width: 0; }
                                 """;
    static void StylingContent(Gui gui)
    {
        using (gui.StyledNode("VisualElement", id: "root").Expand().Enter())
        {
            gui.DrawText("USS-styled widgets", 24, Color.FromArgb(255, 236, 238, 243));

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
            }
        }
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

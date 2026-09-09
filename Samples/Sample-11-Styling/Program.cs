using Guinevere;
using Guinevere.OpenGL.SilkNET;

namespace Sample_11_Styling;

/// <summary>
/// A CSS-like <c>.uss</c> stylesheet drives the widgets — the same styling engine a hosted
/// <c>.ui</c> document uses. Hover a <c>.btn</c> to see the <c>:hover</c> rule.
/// </summary>
public abstract class Program
{
    private const string Style = """
        /* variables */
        --bg:        #12151d;
        --card:      #202634;
        --btn:       #39435a;
        --btn-hover: #4a90e2;
        --btn-down:  #2f6fc0;
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

    public static void Main()
    {
        var gui = new Gui();
        gui.StyleSheets.Add(StyleSheet.Parse(Style));

        using var win = new GuiWindow(gui, 720, 560, "USS Styling");
        win.RunGui(() => Draw(gui));
    }

    private static void Draw(Gui gui)
    {
        gui.DrawRect(gui.ScreenRect, Color.FromArgb(255, 12, 14, 20));
        gui.DrawWindowTitlebar();

        using (gui.StyledNode("VisualElement", id: "root").Expand().Enter())
        {
            gui.DrawText("USS-styled widgets", 24, Color.FromArgb(255, 236, 238, 243));

            using (gui.StyledNode("VisualElement", new[] { "card" }).Enter())
            {
                gui.DrawText("A .card panel — padding, radius and background from the sheet.", 14,
                    Color.FromArgb(255, 154, 160, 166));

                using (gui.StyledNode("VisualElement", new[] { "row" }).Enter())
                {
                    Btn(gui, "Normal", null);
                    Btn(gui, "Primary", "primary");
                    Btn(gui, "Also .btn", null);
                }
            }
        }
    }

    private static void Btn(Gui gui, string label, string? id)
    {
        var classes = new[] { "btn" };
        using (gui.StyledNode("Button", classes, id).Enter())
        {
            _ = gui.GetInteractable().OnClick();
            gui.DrawText(label, 15, Color.FromArgb(255, 236, 238, 243));
        }
    }
}

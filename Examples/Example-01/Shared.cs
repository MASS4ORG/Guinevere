using Guinevere;
using GuinevereDemos;

namespace Example_01;

public partial class Shared(Gui gui)
{
    static readonly Color tabColor = Color.PaleVioletRed;
    static readonly Color tabColorSelected = Color.Pink;

    static readonly List<(string, Action<Gui>)> panels = [
        ("ChildrenLayout", ChildrenLayout),
        ("Constrains", Constrains),
        ("Text - Basic", BasicText),
        ("Text - Wrapping", Wrapping),
        ("Text - Color", ColorDemo),
        ("Text - Effects", TextEffects),
        ("Text - Long Content", LongContent),
        ("Animation", Animation),
        ("DragAndDrop", DragAndDrop),
        ("Styling", Styling),
        ("HeadlessControls", HeadlessControls),
        ("EventsAndCursors", EventsAndCursors),
    ];

    static int panelIndex;

    static public void Panel1(Gui gui)
    {
        using (gui.Node(150, 150).Enter())
        {
            gui.DrawBackgroundRect(Color.PaleVioletRed);
        }
    }
    static public void Panel2(Gui gui)
    {
        using (gui.Node(150, 150).Enter())
        {
            gui.DrawBackgroundRect(Color.PaleVioletRed);
        }
    }

    public void Draw()
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Enter())
        {
            DemoHeader.Header(gui, "Guinevere");

            Tabs(gui);

            using (gui.Node().Expand().Enter())
            {
                gui.DrawBackgroundRect(gui.StyleSheets.GetTokenColor("surface"));
                panels[panelIndex].Item2(gui);
            }
        }
    }

    static void Tabs(Gui gui)
    {
        using (gui.Node()
            .ExpandWidth()
            .Gap(10)
            .Direction(Axis.Horizontal)
            .Wrap(1)
            .Enter())
        {
            gui.DrawBackgroundRect(gui.StyleSheets.GetTokenColor("surface"));
            for (var i = 0; i < panels.Count; i++)
            {
                using (gui.Node().Padding(2).Enter())
                {
                    gui.DrawBackgroundRect(panelIndex == i ?
                        tabColorSelected : tabColor);
                    gui.DrawRectBorder(gui.CurrentNode.Rect, tabColorSelected);
                    gui.DrawText(panels[i].Item1);

                    if (gui.GetInteractable().OnClick())
                        panelIndex = i;
                }
            }
        }
    }
}

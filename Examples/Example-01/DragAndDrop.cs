using Guinevere;

namespace Example_01;

public partial class Shared
{
    readonly record struct Card(string Id, string Label);
    static List<Card> cards =
    [
        new("One", "Typed payload"),
        new("Two", "Preview before drop"),
        new("Three", "Keyboard accessible")
    ];
    // static Color? Token(Gui gui, string name) => gui.StyleSheets.GetTokenColor(name);

    static DragDropTag cardChannel = new("demo/cards");
    static string? lastDrop = null;

    static void DragAndDrop(Gui gui)
    {
        using (gui.Node().Expand().Direction(Axis.Horizontal).Padding(24).Gap(24).Enter())
        {
            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(8).Enter())
            {
                gui.DrawText("Sources", 20, gui.StyleSheets.GetTokenColor("text"));
                foreach (var card in cards)
                {
                    using (gui.Node(-1, 54, $"card/{card.Id}").ExpandWidth().Padding(10).Enter())
                    {
                        gui.DrawBackgroundRect(gui.StyleSheets.GetTokenColor("surface"), 5);
                        gui.DrawText(card.Label, 14, gui.StyleSheets.GetTokenColor("text"));
                        gui.DragSource($"card/{card.Id}", card, tag: cardChannel, ghost: g =>
                        {
                            using (g.Node(170, 36).Padding(8).Enter())
                            {
                                g.DrawBackgroundRect(gui.StyleSheets.GetTokenColor("selected"), 4);
                                g.DrawText(card.Label, 13, gui.StyleSheets.GetTokenColor("text-on-accent"));
                            }
                        });
                    }
                }
            }

            using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
            {
                gui.DrawText("Targets", 20, gui.StyleSheets.GetTokenColor("text"));
                Target(gui, "images", "Accepts cards One and Two", cardChannel,
                    card => card.Id != "Three", card => lastDrop = $"{card.Label} → images");
                Target(gui, "archive", "Accepts every card", cardChannel,
                    _ => true, card => lastDrop = $"{card.Label} → archive");

                if (lastDrop is not null) gui.DrawText(lastDrop, 14, gui.StyleSheets.GetTokenColor("positive"));
            }
        }

        gui.DragGhost();
    }

    static void Target(Gui gui, string id, string label, DragDropTag tag,
        Func<Card, bool> accept, Action<Card> drop)
    {
        using (gui.Node(-1, 90, id).ExpandWidth().Padding(12).Enter())
        {
            gui.DrawBackgroundRect(gui.StyleSheets.GetTokenColor("surface"), 6);
            gui.DrawText(label, 14, gui.StyleSheets.GetTokenColor("text"));
            var result = gui.DropTarget(id, tag, accept, drop);
            gui.DrawDropIndicator(result.State, style: new(
                    gui.StyleSheets.GetTokenColor("--positive"), gui.StyleSheets.GetTokenColor("--negative"))
            );
            if (result.Payload is { } card && result.State != DropTargetState.None)
                gui.DrawText($"Preview: {card.Label} ({result.State})", 12, gui.StyleSheets.GetTokenColor("text-dim"));
        }
    }
}

using Guinevere;

var gui = new Gui();
using var window = new GuiWindow(gui, 720, 420, "Typed drag and drop");
var cards = new List<Card>
{
    new("One", "Typed payload"),
    new("Two", "Preview before drop"),
    new("Three", "Keyboard accessible")
};
var cardChannel = new DragDropTag("demo/cards");
string? lastDrop = null;

window.RunGui(() =>
{
    using (gui.Node().Expand().Direction(Axis.Horizontal).Padding(24).Gap(24).Enter())
    {
        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(8).Enter())
        {
            gui.DrawText("Sources", 20, Token(gui, "text"));
            foreach (var card in cards)
            {
                using (gui.Node(-1, 54, $"card/{card.Id}").ExpandWidth().Padding(10).Enter())
                {
                    gui.DrawBackgroundRect(Token(gui, "surface"), 5);
                    gui.DrawText(card.Label, 14, Token(gui, "text"));
                    gui.DragSource($"card/{card.Id}", card, tag: cardChannel, ghost: g =>
                    {
                        using (g.Node(170, 36).Padding(8).Enter())
                        {
                            g.DrawBackgroundRect(Token(g, "selected"), 4);
                            g.DrawText(card.Label, 13, Token(g, "text-on-accent"));
                        }
                    });
                }
            }
        }

        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
        {
            gui.DrawText("Targets", 20, Token(gui, "text"));
            Target(gui, "images", "Accepts cards One and Two", cardChannel,
                card => card.Id != "Three", card => lastDrop = $"{card.Label} → images");
            Target(gui, "archive", "Accepts every card", cardChannel,
                _ => true, card => lastDrop = $"{card.Label} → archive");

            if (lastDrop is not null) gui.DrawText(lastDrop, 14, Token(gui, "positive"));
        }
    }

    gui.DragGhost();
});

static Color Token(Gui gui, string name) => ExcaliburStyles.TokenColor(gui, name);

static void Target(Gui gui, string id, string label, DragDropTag tag,
    Func<Card, bool> accept, Action<Card> drop)
{
    using (gui.Node(-1, 90, id).ExpandWidth().Padding(12).Enter())
    {
        gui.DrawBackgroundRect(Token(gui, "surface"), 6);
        gui.DrawText(label, 14, Token(gui, "text"));
        var result = gui.DropTarget(id, tag, accept, drop);
        gui.DrawDropIndicator(result.State, style: ExcaliburStyles.DroppableArea(gui));
        if (result.Payload is { } card && result.State != DropTargetState.None)
            gui.DrawText($"Preview: {card.Label} ({result.State})", 12, Token(gui, "text-dim"));
    }
}

readonly record struct Card(string Id, string Label);

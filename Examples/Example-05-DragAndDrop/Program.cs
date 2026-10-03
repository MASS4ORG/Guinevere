using Guinevere;

var gui = new Gui { ControlPalette = ControlPalette.Dark };
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
            gui.DrawText("Sources", 20, gui.ControlPalette.Text);
            foreach (var card in cards)
            {
                using (gui.Node(-1, 54, $"card/{card.Id}").ExpandWidth().Padding(10).Enter())
                {
                    gui.DrawBackgroundRect(gui.ControlPalette.Surface, 5);
                    gui.DrawText(card.Label, 14, gui.ControlPalette.Text);
                    gui.DragSource($"card/{card.Id}", card, tag: cardChannel, ghost: g =>
                    {
                        using (g.Node(170, 36).Padding(8).Enter())
                        {
                            g.DrawBackgroundRect(g.ControlPalette.Selected, 4);
                            g.DrawText(card.Label, 13, g.ControlPalette.TextOnAccent);
                        }
                    });
                }
            }
        }

        using (gui.Node().Expand().Direction(Axis.Vertical).Gap(12).Enter())
        {
            gui.DrawText("Targets", 20, gui.ControlPalette.Text);
            Target(gui, "images", "Accepts cards One and Two", cardChannel,
                card => card.Id != "Three", card => lastDrop = $"{card.Label} → images");
            Target(gui, "archive", "Accepts every card", cardChannel,
                _ => true, card => lastDrop = $"{card.Label} → archive");

            if (lastDrop is not null) gui.DrawText(lastDrop, 14, gui.ControlPalette.Positive);
        }
    }

    gui.DragGhost();
});

static void Target(Gui gui, string id, string label, DragDropTag tag,
    Func<Card, bool> accept, Action<Card> drop)
{
    using (gui.Node(-1, 90, id).ExpandWidth().Padding(12).Enter())
    {
        gui.DrawBackgroundRect(gui.ControlPalette.Surface, 6);
        gui.DrawText(label, 14, gui.ControlPalette.Text);
        var result = gui.DropTarget(id, tag, accept, drop);
        gui.DrawDropIndicator(result.State);
        if (result.Payload is { } card && result.State != DropTargetState.None)
            gui.DrawText($"Preview: {card.Label} ({result.State})", 12, gui.ControlPalette.TextDim);
    }
}

readonly record struct Card(string Id, string Label);

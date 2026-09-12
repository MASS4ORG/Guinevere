namespace Guinevere;

/// <summary>What the user did to an <see cref="ControlsExtensions.ObjectField"/> this frame.</summary>
public enum ObjectFieldAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>The pick button was clicked, which should open a picker.</summary>
    Pick,

    /// <summary>The clear button was clicked.</summary>
    Clear,

    /// <summary>The value box was clicked, which should select the referenced object where it lives.</summary>
    Reveal,

    /// <summary>A payload the caller accepted was dropped on the field.</summary>
    Drop,
}

/// <summary>The outcome of one <see cref="ControlsExtensions.ObjectField"/> call.</summary>
/// <param name="Action">What the user did.</param>
/// <param name="Payload">The dropped payload when <paramref name="Action"/> is a drop.</param>
public readonly record struct ObjectFieldResult(ObjectFieldAction Action, object? Payload = null);

public static partial class ControlsExtensions
{
    /// <summary>
    /// A Unity-style reference slot: a box naming what is currently referenced, which accepts a
    /// dropped payload, reveals its value when clicked and opens a picker from its own button. The
    /// control holds no opinion about what a reference is — <paramref name="accept"/> decides what may
    /// land on it.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="text">What the current value is called.</param>
    /// <param name="id">Stable id, unique among siblings.</param>
    /// <param name="accept">Whether a dragged payload may be dropped here.</param>
    /// <param name="isEmpty">Whether the reference points at nothing, which dims the text.</param>
    /// <param name="showClear">Whether to offer a clear button.</param>
    /// <param name="showPick">Whether to offer the button that opens a picker.</param>
    /// <param name="height">Row height.</param>
    /// <param name="fontSize">Text size.</param>
    /// <returns>What the user did this frame.</returns>
    public static ObjectFieldResult ObjectField(this Gui gui, string text, string id,
        Func<object, bool>? accept = null,
        bool isEmpty = false,
        bool showClear = true,
        bool showPick = true,
        float height = 20,
        float fontSize = 12)
    {
        ArgumentNullException.ThrowIfNull(gui);

        var palette = gui.Controls;
        var action = ObjectFieldAction.None;
        object? dropped = null;

        using (gui.Node(-1, height, id).ExpandWidth().Direction(Axis.Horizontal).Enter())
        {
            var hovering = gui.DropTarget(id, accept, payload =>
            {
                dropped = payload;
                action = ObjectFieldAction.Drop;
            });

            var interactable = gui.GetInteractable();
            var border = hovering ? palette.Accent : palette.Border;
            var fill = interactable.OnHover() ? palette.SurfaceHover : palette.Surface;

            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                gui.DrawRect(rect, fill, 3);
                gui.DrawRectBorder(rect, border, hovering ? 2f : 1f, 3);
            }

            using (gui.Node(-1, height).Expand().Padding(6, 0).ContentAlignY(0.5f).Enter())
                gui.DrawText(text, fontSize, isEmpty ? palette.TextDim : palette.Text);

            if (showPick && GlyphButton(gui, $"{id}/pick", "◎", height, palette)) action = ObjectFieldAction.Pick;
            if (showClear && !isEmpty && GlyphButton(gui, $"{id}/clear", "×", height, palette))
                action = ObjectFieldAction.Clear;

            // Those buttons sit inside the box, so a click on one must not also read as a reveal.
            if (action == ObjectFieldAction.None && !isEmpty && interactable.OnClick())
                action = ObjectFieldAction.Reveal;
        }

        return new ObjectFieldResult(action, dropped);
    }

    /// <summary>One of the slot's inline buttons. Blocks input so it never also hits the box behind it.</summary>
    private static bool GlyphButton(Gui gui, string id, string glyph, float height, ControlPalette palette)
    {
        using (gui.Node(height, height, id).BlockInput().Enter())
        {
            var interactable = gui.GetInteractable();
            if (gui.Pass == Pass.Pass2Render)
                gui.DrawText(glyph, height * 0.7f, interactable.OnHover() ? palette.Text : palette.TextDim);

            return interactable.OnClick();
        }
    }
}

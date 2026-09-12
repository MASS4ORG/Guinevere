namespace Guinevere;

/// <summary>What the user did to an <see cref="ControlsExtensions.ObjectField"/> this frame.</summary>
public enum ObjectFieldAction
{
    /// <summary>Nothing happened.</summary>
    None,

    /// <summary>The value box was clicked, which should open a picker.</summary>
    Pick,

    /// <summary>The clear button was clicked.</summary>
    Clear,

    /// <summary>The value box was double-clicked, which should reveal the referenced object.</summary>
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
    /// dropped payload and opens a picker when clicked. The control holds no opinion about what a
    /// reference is — <paramref name="accept"/> decides what may land on it.
    /// </summary>
    /// <param name="gui">The GUI for this frame.</param>
    /// <param name="text">What the current value is called.</param>
    /// <param name="id">Stable id, unique among siblings.</param>
    /// <param name="accept">Whether a dragged payload may be dropped here.</param>
    /// <param name="isEmpty">Whether the reference points at nothing, which dims the text.</param>
    /// <param name="showClear">Whether to offer a clear button.</param>
    /// <param name="height">Row height.</param>
    /// <param name="fontSize">Text size.</param>
    /// <returns>What the user did this frame.</returns>
    public static ObjectFieldResult ObjectField(this Gui gui, string text, string id,
        Func<object, bool>? accept = null,
        bool isEmpty = false,
        bool showClear = true,
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

            if (showClear && !isEmpty && ClearButton(gui, height, palette)) action = ObjectFieldAction.Clear;

            // The clear button sits inside the box, so a click on it must not also read as a pick.
            if (action == ObjectFieldAction.None && interactable.OnClick())
                action = ObjectFieldAction.Pick;
        }

        return new ObjectFieldResult(action, dropped);
    }

    private static bool ClearButton(Gui gui, float height, ControlPalette palette)
    {
        using (gui.Node(height, height, "clear").BlockInput().Enter())
        {
            var interactable = gui.GetInteractable();
            if (gui.Pass == Pass.Pass2Render)
                gui.DrawText("×", height * 0.7f, interactable.OnHover() ? palette.Text : palette.TextDim);

            return interactable.OnClick();
        }
    }
}

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
    /// <summary>What an <see cref="ControlsExtensions.ObjectField"/> remembers between frames.</summary>
    sealed class ObjectFieldState
    {
        /// <summary>A payload dropped here, waiting to be reported.</summary>
        public object? Dropped { get; set; }
    }

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
    /// <returns>
    /// What the user did this frame. A drop is reported on the frame after the pointer is released,
    /// because a drag only settles once the frame it ended in is over.
    /// </returns>
    public static ObjectFieldResult ObjectField(this Gui gui, string text, string id,
        Func<object, bool>? accept = null,
        bool isEmpty = false,
        bool showClear = true,
        bool showPick = true,
        float height = ControlMetrics.IndicatorSize,
        float fontSize = ControlMetrics.CompactFontSize)
    {
        ArgumentNullException.ThrowIfNull(gui);

        var palette = gui.Controls;
        var state = gui.ControlState(id, () => new ObjectFieldState());
        var action = ObjectFieldAction.None;

        using (gui.Node(-1, height, id).ExpandWidth().Direction(Axis.Horizontal).Enter())
        {
            var drop = gui.DropTarget(id, canAccept: accept, onDrop: payload => state.Dropped = payload);

            var interactable = gui.GetInteractable();
            var fill = interactable.OnHover() ? palette.SurfaceHover : palette.Surface;

            var eligibility = DropEligibility(gui, accept);
            var border = eligibility switch
            {
                true => palette.Positive,
                false => palette.Negative,
                null => palette.Border
            };

            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                gui.DrawRect(rect, fill, 3);
                gui.DrawRectBorder(rect, border, drop.IsAccepted ? 2.5f : eligibility is not null ? 2f : 1f, 3);
            }

            using (gui.Node(-1, height).Expand().Padding(6, 0).ContentAlignY(0.5f).Enter())
                gui.DrawText(text, fontSize, isEmpty ? palette.TextDim : palette.Text);

            if (showPick && PickButton(gui, $"{id}/pick", height, palette)) action = ObjectFieldAction.Pick;
            if (showClear && !isEmpty && GlyphButton(gui, $"{id}/clear", "×", height, palette))
                action = ObjectFieldAction.Clear;

            // Those buttons sit inside the box, so a click on one must not also read as a reveal.
            if (gui.Pass == Pass.Pass2Render && action == ObjectFieldAction.None && !isEmpty
                && interactable.OnClick())
                action = ObjectFieldAction.Reveal;
        }

        if (gui.Pass != Pass.Pass2Render || action != ObjectFieldAction.None || state.Dropped is not { } parked)
            return new ObjectFieldResult(action);

        state.Dropped = null;
        return new ObjectFieldResult(ObjectFieldAction.Drop, parked);
    }

    /// <summary>
    /// Whether the drag in progress would be accepted here: true for yes, false for no, null when
    /// nothing is being dragged and the slot should look ordinary.
    /// </summary>
    static bool? DropEligibility(Gui gui, Func<object, bool>? accept)
    {
        if (gui.CurrentDragPayload is not { } payload) return null;

        return accept?.Invoke(payload) ?? true;
    }

    /// <summary>
    /// The button that opens the picker, drawn as a target rather than typed: the system fonts in play
    /// carry no "◎". Blocks input so it never also hits the box behind it.
    /// </summary>
    static bool PickButton(Gui gui, string id, float height, ControlPalette palette)
    {
        using (gui.Node(height, height, id).BlockInput().Enter())
        {
            var interactable = gui.GetInteractable();

            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                var center = new Vector2(rect.X + (rect.W / 2f), rect.Y + (rect.H / 2f));
                var color = interactable.OnHover() ? palette.Text : palette.TextDim;

                gui.DrawCircleBorder(center, height * 0.28f, color);
                gui.DrawCircleFilled(center, height * 0.1f, color);
            }

            return gui.Pass == Pass.Pass2Render && interactable.OnClick();
        }
    }

    /// <summary>One of the slot's inline buttons. Blocks input so it never also hits the box behind it.</summary>
    static bool GlyphButton(Gui gui, string id, string glyph, float height, ControlPalette palette)
    {
        using (gui.Node(height, height, id).BlockInput().Enter())
        {
            var interactable = gui.GetInteractable();
            gui.DrawText(glyph, height * 0.7f, interactable.OnHover() ? palette.Text : palette.TextDim);

            return gui.Pass == Pass.Pass2Render && interactable.OnClick();
        }
    }
}

using System.Runtime.CompilerServices;

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
    /// <summary>What an <see cref="ObjectField"/> remembers between frames.</summary>
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
    /// <param name="result">
    /// What the user did this frame. A drop is reported on the frame after the pointer is released,
    /// because a drag only settles once the frame it ended in is over.
    /// </param>
    /// <param name="accept">Whether a dragged payload may be dropped here.</param>
    /// <param name="isEmpty">Whether the reference points at nothing, which dims the text.</param>
    /// <param name="showClear">Whether to offer a clear button.</param>
    /// <param name="showPick">Whether to offer the button that opens a picker.</param>
    /// <param name="enabled">When false the field matches <c>:disabled</c> and reports nothing.</param>
    /// <param name="classes">Stylesheet classes for the field.</param>
    /// <param name="id">Optional id for <c>object-field#id</c> rules and drop and state identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The field's node: <see cref="LayoutNode.Clicked"/> for a pick or reveal and <see cref="LayoutNode.Changed"/>
    /// for a clear or drop (as a <c>bool</c>, any of them). It fills the parent's width at the scope's indicator
    /// height unless a sheet or the node says otherwise.
    /// </returns>
    public static ControlNode ObjectField(this Gui gui, string text, out ObjectFieldResult result,
        Func<object, bool>? accept = null,
        bool isEmpty = false,
        bool showClear = true,
        bool showPick = true,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);

        ExcaliburStyles.Ensure(gui);
        var side = gui.ControlStyle.IndicatorSize;
        var node = gui.StyledControl("object-field", classes, id,
            ObjectFieldModifiers(gui, enabled, isEmpty, accept), disabled: !enabled,
            filePath: filePath, lineNumber: lineNumber);
        UseFontSize(gui, node, gui.ControlStyle.CompactFontSize);
        SizeUnlessStyled(node, Size.Expand(), side);
        node.Direction(Axis.Horizontal);
        var state = gui.ControlState(node.Id, static () => new ObjectFieldState());

        var action = RenderObjectFieldNode(gui, node, state, text, accept, isEmpty, showClear, showPick, enabled,
            side);
        result = ResolveObjectFieldResult(gui, state, action);
        RaiseObjectFieldSignals(gui, node, result.Action);
        return node;
    }

    /// <summary>Modifiers for an empty field and for a drag hovering it, accepting or rejecting.</summary>
    static List<string> ObjectFieldModifiers(Gui gui, bool enabled, bool isEmpty, Func<object, bool>? accept)
    {
        var modifiers = new List<string>();
        if (isEmpty)
        {
            modifiers.Add("empty");
        }

        var eligibility = enabled ? DropEligibility(gui, accept) : null;
        if (eligibility is { } eligible)
        {
            modifiers.Add(eligible ? "accepting" : "rejecting");
        }

        return modifiers;
    }

    /// <summary>Draws the box, text, buttons and reveal target, returning the action the user took.</summary>
    static ObjectFieldAction RenderObjectFieldNode(Gui gui, ControlNode node, ObjectFieldState state, string text,
        Func<object, bool>? accept, bool isEmpty, bool showClear, bool showPick, bool enabled, float side)
    {
        var action = ObjectFieldAction.None;
        using (node.Enter())
        {
            var fontSize = FieldFontSize(gui);
            var drop = gui.DropTarget(node.Id, canAccept: DropAcceptance(enabled, accept),
                onDrop: ParkDroppedPayload(state));

            var interactable = gui.GetInteractable();
            if (drop.IsAccepted)
            {
                gui.DrawStyledBox(gui.ResolvePart("drop-outline", modifiers: ["accepted"]), gui.CurrentNode.Rect);
            }

            using (gui.Node(-1, side).Expand().Padding(6, 0).AlignContentY(0.5f).Enter())
            {
                gui.ClipContent();
                gui.DrawText(text, fontSize);
            }

            action = ObjectFieldButtons(gui, side, enabled, isEmpty, showPick, showClear);

            // Those buttons sit inside the box, so a click on one must not also read as a reveal.
            if (gui.Pass == Pass.Pass2Render && enabled && action == ObjectFieldAction.None && !isEmpty
                && interactable.OnClick())
            {
                action = ObjectFieldAction.Reveal;
            }
        }

        return action;
    }

    /// <summary>Draws the pick and clear buttons and returns the one clicked, with clear taking precedence.</summary>
    static ObjectFieldAction ObjectFieldButtons(Gui gui, float side, bool enabled, bool isEmpty, bool showPick,
        bool showClear)
    {
        var action = ObjectFieldAction.None;
        if (showPick && PickButton(gui, side, enabled))
        {
            action = ObjectFieldAction.Pick;
        }

        if (showClear && !isEmpty && GlyphButton(gui, "×", side, enabled))
        {
            action = ObjectFieldAction.Clear;
        }

        return action;
    }

    /// <summary>What a drop may land on: the caller's filter while enabled, and nothing when disabled.</summary>
    static Func<object, bool>? DropAcceptance(bool enabled, Func<object, bool>? accept) =>
        enabled ? accept : static _ => false;

    /// <summary>The drop callback that parks a payload on the field for the next render pass to report.</summary>
    static Action<object> ParkDroppedPayload(ObjectFieldState state) => payload => state.Dropped = payload;

    /// <summary>Reports a parked drop when no button acted this frame, then clears it.</summary>
    static ObjectFieldResult ResolveObjectFieldResult(Gui gui, ObjectFieldState state, ObjectFieldAction action)
    {
        if (gui.Pass == Pass.Pass2Render && action == ObjectFieldAction.None && state.Dropped is { } parked)
        {
            state.Dropped = null;
            return new ObjectFieldResult(ObjectFieldAction.Drop, parked);
        }

        return new ObjectFieldResult(action);
    }

    /// <summary>Maps what the user did to the node's signals: picking and revealing click, clearing and dropping change.</summary>
    static void RaiseObjectFieldSignals(Gui gui, LayoutNode node, ObjectFieldAction action)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        switch (action)
        {
            case ObjectFieldAction.Pick or ObjectFieldAction.Reveal:
                node.Raise(NodeSignals.Clicked);
                break;
            case ObjectFieldAction.Clear or ObjectFieldAction.Drop:
                node.Raise(NodeSignals.Changed);
                break;
        }
    }

    /// <summary>
    /// Whether the drag in progress would be accepted here: true for yes, false for no, null when
    /// nothing is being dragged and the slot should look ordinary.
    /// </summary>
    static bool? DropEligibility(Gui gui, Func<object, bool>? accept)
    {
        if (gui.CurrentDragPayload is not { } payload)
        {
            return null;
        }

        return accept?.Invoke(payload) ?? true;
    }

    /// <summary>
    /// The button that opens the picker, drawn as a target rather than typed: the system fonts in play
    /// carry no "◎". Blocks input so it never also hits the box behind it.
    /// </summary>
    static bool PickButton(Gui gui, float side, bool enabled)
    {
        using (gui.StyledNode("object-pick", disabled: !enabled).Width(side).Height(side).BlockInput().Enter())
        {
            var interactable = gui.GetInteractable();

            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                var center = new Vector2(rect.X + (rect.W / 2f), rect.Y + (rect.H / 2f));
                var color = gui.CurrentNode.Scope.Get<LayoutNodeScopeTextColor>().Value;

                gui.DrawCircleBorder(center, side * 0.28f, color);
                gui.DrawCircleFilled(center, side * 0.1f, color);
            }

            return gui.Pass == Pass.Pass2Render && enabled && interactable.OnClick();
        }
    }

    /// <summary>One of the slot's inline buttons. Blocks input so it never also hits the box behind it.</summary>
    static bool GlyphButton(Gui gui, string glyph, float side, bool enabled)
    {
        using (gui.StyledNode("object-clear", disabled: !enabled).Width(side).Height(side).BlockInput().Enter())
        {
            var interactable = gui.GetInteractable();
            gui.DrawText(glyph, side * 0.7f);

            return gui.Pass == Pass.Pass2Render && enabled && interactable.OnClick();
        }
    }
}

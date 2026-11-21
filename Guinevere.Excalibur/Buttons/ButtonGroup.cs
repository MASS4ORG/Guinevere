using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>How a button group treats activation of its items.</summary>
    enum ButtonGroupMode { Action, Single, Multiple }

    /// <summary>Where an item sits in its group, which the sheet reads as a class to join corners and borders.</summary>
    enum ButtonGroupPosition { Only, First, Middle, Last }

    /// <summary>The orientation and position classes of every item, indexed by vertical (4) plus position.</summary>
    static readonly string[][] ButtonGroupItemClasses =
    [
        ["horizontal", "only"], ["horizontal", "first"], ["horizontal", "middle"], ["horizontal", "last"],
        ["vertical", "only"], ["vertical", "first"], ["vertical", "middle"], ["vertical", "last"]
    ];

    /// <summary>
    /// Attached buttons that act on click: items sit edge to edge as one control, and the item that was activated this
    /// frame is reported through <paramref name="clickedIndex"/>. Tab enters and leaves the group as one stop; Left/Right
    /// (Up/Down when vertical) move between enabled items, wrapping, Home/End jump to the ends, and Space or Enter
    /// activates the focused item. Styled by <c>button-group</c> (class <c>vertical</c> when stacked) and its
    /// <c>button-group-item</c> children, which carry <c>horizontal</c> or <c>vertical</c> plus <c>only</c>, <c>first</c>,
    /// <c>middle</c> or <c>last</c> so the sheet can round and join the outer edges only.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="items">What each item shows, in order; an item may be disabled.</param>
    /// <param name="clickedIndex">The index of the item activated this frame, otherwise -1; set in the render pass.</param>
    /// <param name="orientation">Whether the items sit side by side or stacked.</param>
    /// <param name="enabled">When false every item matches <c>:disabled</c> and never activates.</param>
    /// <param name="classes">Extra classes for the group, so <c>button-group.x button-group-item</c> rules reach its items.</param>
    /// <param name="id">Optional id for <c>button-group#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The group's node, <c>true</c> as a <c>bool</c> (and <see cref="LayoutNode.Clicked"/> after a chain) on the frame an
    /// item is activated.
    /// </returns>
    public static ControlNode ButtonGroup(this Gui gui, IReadOnlyList<ButtonGroupItem> items, out int clickedIndex,
        Axis orientation = Axis.Horizontal,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var unused = -1;
        return ButtonGroupCore(gui, ButtonGroupMode.Action, items, ref unused, null, orientation, enabled, classes, id,
            filePath, lineNumber, out clickedIndex);
    }

    /// <summary>
    /// A segmented control: attached items of which exactly one is selected, written into
    /// <paramref name="selectedIndex"/> when the user activates another. Arrow keys move focus only; Space or Enter
    /// selects the focused item, so keyboard users can browse without firing a change per key press. While focus is
    /// outside the group its single Tab stop rests on the selected item. Styling and keyboard movement are those of
    /// <see cref="ButtonGroup(Gui, IReadOnlyList{ButtonGroupItem}, out int, Axis, bool, IReadOnlyList{string}?, string?, string, int)"/>;
    /// selected items also match <c>:checked</c>.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="selectedIndex">The selected item; -1 selects none. Set when another item is activated.</param>
    /// <param name="items">What each item shows, in order; an item may be disabled.</param>
    /// <param name="orientation">Whether the items sit side by side or stacked.</param>
    /// <param name="enabled">When false every item matches <c>:disabled</c> and the selection never changes.</param>
    /// <param name="classes">Extra classes for the group.</param>
    /// <param name="id">Optional id for <c>button-group#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The group's node, <c>true</c> as a <c>bool</c> on the frame an item is activated; <see cref="LayoutNode.Changed"/>
    /// only when that moved the selection.
    /// </returns>
    public static ControlNode ButtonGroup(this Gui gui, ref int selectedIndex, IReadOnlyList<ButtonGroupItem> items,
        Axis orientation = Axis.Horizontal,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0) =>
        ButtonGroupCore(gui, ButtonGroupMode.Single, items, ref selectedIndex, null, orientation, enabled, classes, id,
            filePath, lineNumber, out _);

    /// <summary>
    /// Attached toggle buttons of which any number are selected: activating an item adds its index to
    /// <paramref name="selected"/> or removes it. Keyboard behavior is that of a segmented control; selected items
    /// match <c>:checked</c>. The caller owns the set and may replace its contents between frames.
    /// </summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="selected">The indices of the selected items, edited in place when the user toggles an item.</param>
    /// <param name="items">What each item shows, in order; an item may be disabled.</param>
    /// <param name="orientation">Whether the items sit side by side or stacked.</param>
    /// <param name="enabled">When false every item matches <c>:disabled</c> and the set never changes.</param>
    /// <param name="classes">Extra classes for the group.</param>
    /// <param name="id">Optional id for <c>button-group#id</c> rules and a stable identity; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>
    /// The group's node, <c>true</c> as a <c>bool</c> on the frame an item is toggled (also
    /// <see cref="LayoutNode.Changed"/>, since every activation edits the set).
    /// </returns>
    public static ControlNode ButtonGroup(this Gui gui, ISet<int> selected, IReadOnlyList<ButtonGroupItem> items,
        Axis orientation = Axis.Horizontal,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(selected);
        var unused = -1;
        return ButtonGroupCore(gui, ButtonGroupMode.Multiple, items, ref unused, selected, orientation, enabled,
            classes, id, filePath, lineNumber, out _);
    }

    static ControlNode ButtonGroupCore(Gui gui, ButtonGroupMode mode, IReadOnlyList<ButtonGroupItem> items,
        ref int selectedIndex, ISet<int>? selectedSet, Axis orientation, bool enabled,
        IReadOnlyList<string>? classes, string? id, string filePath, int lineNumber, out int activated)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(items);
        ExcaliburStyles.Ensure(gui);
        var key = id ?? gui.AutomaticId(filePath, lineNumber);
        var state = gui.ControlState(key, static () => new ButtonGroupState());
        var vertical = orientation == Axis.Vertical;
        var group = gui.StyledControl("button-group", vertical ? WithClasses(VerticalClass, classes) : classes, key,
            NoModifiers, disabled: !enabled, filePath: filePath, lineNumber: lineNumber);
        state.Begin(items, enabled, vertical, mode == ButtonGroupMode.Single ? selectedIndex : -1,
            gui.HasFocusWithin(group.Id));
        UseControlFontSize(gui, group);
        FitUnlessStyled(group);
        group.Direction(orientation).On(state.KeyHandler);

        activated = -1;
        using (group.Enter())
        {
            for (var i = 0; i < items.Count; i++)
            {
                var checkedItem = IsButtonGroupItemChecked(mode, i, selectedIndex, selectedSet);
                if (ButtonGroupItemInput(gui, group, state, items[i], i, items.Count, vertical, checkedItem, mode))
                {
                    activated = i;
                    ApplyButtonGroupActivation(gui, group, mode, i, ref selectedIndex, selectedSet);
                }
            }
        }

        return group;
    }

    /// <summary>Whether the item at <paramref name="index"/> shows as selected in the group's mode.</summary>
    static bool IsButtonGroupItemChecked(ButtonGroupMode mode, int index, int selectedIndex, ISet<int>? selectedSet) =>
        mode switch
        {
            ButtonGroupMode.Single => index == selectedIndex,
            ButtonGroupMode.Multiple => selectedSet!.Contains(index),
            _ => false
        };

    /// <summary>Records the activation on the caller's selection and raises the group's signals.</summary>
    static void ApplyButtonGroupActivation(Gui gui, ControlNode group, ButtonGroupMode mode, int index,
        ref int selectedIndex, ISet<int>? selectedSet)
    {
        group.Raise(NodeSignals.Clicked);
        var changed = mode switch
        {
            ButtonGroupMode.Single => selectedIndex != index,
            ButtonGroupMode.Multiple => true,
            _ => false
        };
        if (!changed)
        {
            return;
        }

        if (mode == ButtonGroupMode.Single)
        {
            selectedIndex = index;
        }
        else if (!selectedSet!.Remove(index))
        {
            selectedSet.Add(index);
        }

        group.Raise(NodeSignals.Changed);
        gui.RequestFrame();
    }

    /// <summary>
    /// Builds one item and, in the render pass, registers it as a focus target (the tab stop only, unless it holds
    /// focus), takes focus the arrow keys moved to it and reports whether a click or Space/Enter activated it.
    /// </summary>
    static bool ButtonGroupItemInput(Gui gui, ControlNode group, ButtonGroupState state, ButtonGroupItem item,
        int index, int count, bool vertical, bool isChecked, ButtonGroupMode mode)
    {
        var enabled = state.Enabled[index];
        var node = gui.StyledNode("button-group-item", ButtonGroupItemClassesAt(vertical, index, count),
            state.ItemId(group.Id, item.Id, index), isChecked ? CheckedModifier : NoModifiers,
            disabled: !enabled);
        var field = gui.ControlStyle.FieldHeight;
        SizeUnlessStyled(node, vertical ? Size.Expand() : Size.FitContent(), field);
        PadUnlessStyled(node, gui.ControlStyle.Spacing * 2, 0);
        using (node.Direction(Axis.Horizontal).AlignContent(0.5f).Enter())
        {
            gui.Content(item.Content);
            if (gui.Pass != Pass.Pass2Render)
            {
                return false;
            }

            var role = mode switch
            {
                ButtonGroupMode.Single => ControlRole.Radio,
                ButtonGroupMode.Multiple => ControlRole.Checkbox,
                _ => ControlRole.Button
            };
            var activated = enabled && ButtonGroupItemFocus(gui, state, index);
            PublishMenuSemantics(gui, item.Content.Label ?? "", enabled, isChecked, role);
            return activated;
        }
    }

    /// <summary>
    /// Registers the current item for focus and reports its activation. A click moves the tab stop to the item, and an
    /// item an arrow key moved the stop to asks for focus once it is registered as a target.
    /// </summary>
    static bool ButtonGroupItemFocus(Gui gui, ButtonGroupState state, int index)
    {
        var focused = gui.HasFocus();
        var clicked = gui.GetInteractable().OnClick();
        if (clicked)
        {
            state.Active = index;
        }

        gui.RegisterFocusable(state.Active == index || focused, isInteractable: true);
        if (clicked)
        {
            gui.RequestFocus(FocusReason.Mouse);
        }
        else
        {
            TakePendingButtonGroupFocus(gui, state, index, focused);
        }

        return clicked || focused && IsKeyboardActivated(gui);
    }

    /// <summary>Gives keyboard focus to the item an arrow key moved the tab stop to, once it is a focus target.</summary>
    static void TakePendingButtonGroupFocus(Gui gui, ButtonGroupState state, int index, bool focused)
    {
        if (!state.FocusPending || state.Active != index)
        {
            return;
        }

        state.FocusPending = false;
        if (!focused)
        {
            gui.RequestFocus(FocusReason.Keyboard);
            gui.RequestFrame();
        }
    }

    /// <summary>Whether Space, Enter or the host's activation action fired on the current node this frame.</summary>
    static bool IsKeyboardActivated(Gui gui) =>
        gui.IsAcceptPressed || gui.ControlActivation?.IsActivated(gui.CurrentNode.Id) == true;

    /// <summary>The orientation and position classes of an item, given its index and the group's item count.</summary>
    internal static IReadOnlyList<string> ButtonGroupItemClassesAt(bool vertical, int index, int count)
    {
        var position = count <= 1 ? ButtonGroupPosition.Only
            : index == 0 ? ButtonGroupPosition.First
            : index == count - 1 ? ButtonGroupPosition.Last
            : ButtonGroupPosition.Middle;
        return ButtonGroupItemClasses[(vertical ? 4 : 0) + (int)position];
    }

    /// <summary>The variant classes followed by the caller's, or just the variant when there are none.</summary>
    static IReadOnlyList<string> WithClasses(string[] variant, IReadOnlyList<string>? classes) =>
        classes is null ? variant : [.. variant, .. classes];
}

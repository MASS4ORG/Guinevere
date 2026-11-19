using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>The presentation used by an enum editor.</summary>
public enum EnumPresentation
{
    /// <summary>A searchable dropdown, with checkboxes for flags.</summary>
    Dropdown,
    /// <summary>A horizontal group of selectable buttons.</summary>
    ToggleButtons,
    /// <summary>A searchable dropdown with previous and next buttons cycling declared values.</summary>
    Paging,
}

public static partial class ControlsExtensions
{
    static readonly ConditionalWeakTable<Type, EnumChoices> EnumChoicesByType = [];
    static readonly string[] SegmentClass = ["segment"];

    sealed class EnumChoices
    {
        internal readonly Enum[] Values;
        internal readonly Enum[] SelectionValues;
        internal readonly bool Flags;
        internal readonly Enum Zero;
        internal readonly bool SyntheticZero;

        internal EnumChoices(Type type)
        {
            Values = [.. Enum.GetValues(type).Cast<Enum>().Distinct()];
            Flags = type.IsDefined(typeof(FlagsAttribute), false);
            Zero = (Enum)Enum.ToObject(type, 0);
            SyntheticZero = Flags && !Values.Contains(Zero);
            SelectionValues = SyntheticZero ? [Zero, .. Values] : Values;
        }
    }

    sealed class EnumChoiceState
    {
        internal readonly ChoiceState<Enum> Choices = new();
        internal Enum? FrameValue;
    }

    /// <summary>Draws an enum editor, automatically using multiple selection for flag enums.</summary>
    /// <remarks>
    /// Changes arrive in the build pass, and <see cref="LayoutNode.Changed"/> reads true in that frame's render pass.
    /// Paging cycles declared values and wraps at either end. The control is the scope's field size unless a sheet or the
    /// returned node says otherwise.
    /// </remarks>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The edited value, written when the user changes it.</param>
    /// <param name="presentation">How the choices are shown.</param>
    /// <param name="display">What an option shows; its label is searched. The option name when null.</param>
    /// <param name="maxVisibleItems">How many rows a dropdown popup shows before it scrolls.</param>
    /// <param name="enabled">Whether the editor responds to input.</param>
    /// <param name="mixed">Shows a dash until the user edits a value that differs between owners.</param>
    /// <param name="classes">Extra classes for the control.</param>
    /// <param name="id">Optional id seeding the identity of the control and its parts; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The control's node, <see cref="LayoutNode.Changed"/> (and as a <c>bool</c>) when the value changed.</returns>
    public static ControlNode EnumDropdown<T>(this Gui gui, ref T value,
        EnumPresentation presentation = EnumPresentation.Dropdown, Func<T, Content>? display = null,
        int maxVisibleItems = 6, bool enabled = true, bool mixed = false,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) where T : struct, Enum
    {
        var node = gui.EnumDropdown(value, out var next, out var changes, presentation,
            display is null ? null : option => display((T)option),
            maxVisibleItems, enabled, mixed, classes: classes, id: id, filePath: filePath, lineNumber: lineNumber);
        if (changes.Count > 0)
        {
            value = (T)next;
        }

        return node;
    }

    /// <summary>Draws an enum editor whose individual changes can be applied to every owner of a mixed field.</summary>
    /// <param name="gui">The GUI context.</param>
    /// <param name="value">The current value.</param>
    /// <param name="current">The value with this frame's edits applied.</param>
    /// <param name="changes">The individual edits delivered in the build pass.</param>
    /// <param name="presentation">How the choices are shown.</param>
    /// <param name="display">What an option shows; its label is searched. The option name when null.</param>
    /// <param name="maxVisibleItems">How many rows a dropdown popup shows before it scrolls.</param>
    /// <param name="enabled">Whether the editor responds to input.</param>
    /// <param name="mixed">Shows a dash until the user edits a value that differs between owners.</param>
    /// <param name="isMixed">Whether an option is set in some owners but not all.</param>
    /// <param name="classes">Extra classes for the control.</param>
    /// <param name="id">Optional id seeding the identity of the control and its parts; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The control's node, <see cref="LayoutNode.Changed"/> (and as a <c>bool</c>) when the value changed.</returns>
    public static ControlNode EnumDropdown(this Gui gui, Enum value, out Enum current,
        out IReadOnlyList<SelectionChange<Enum>> changes,
        EnumPresentation presentation = EnumPresentation.Dropdown, Func<Enum, Content>? display = null,
        int maxVisibleItems = 6, bool enabled = true, bool mixed = false, Func<Enum, bool>? isMixed = null,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(value);
        var key = id ?? gui.AutomaticId(filePath, lineNumber);
        var state = gui.ControlState(key, static () => new EnumChoiceState());
        changes = BeginChoices(gui, state.Choices, enabled);
        var metadata = EnumChoicesByType.GetValue(value.GetType(), static type => new EnumChoices(type));
        var multiple = metadata.Flags && presentation != EnumPresentation.Paging;
        PrepareEnumValue(gui, state, value, changes, multiple);
        current = state.FrameValue ?? value;
        var options = presentation == EnumPresentation.Paging ? metadata.Values : metadata.SelectionValues;
        var node = RenderEnumChoice(gui, key, state.Choices, options, current, presentation,
            EnumDisplayOrDefault(display, metadata), EnumSelected(multiple, current, mixed), isMixed, multiple, mixed,
            maxVisibleItems, enabled, classes);
        RaiseEnumChanged(gui, node, state);
        return node;
    }

    /// <summary>The caller's label, or "None" for a flags enum's synthetic zero, else the option name.</summary>
    static Func<Enum, Content> EnumDisplayOrDefault(Func<Enum, Content>? display, EnumChoices metadata)
    {
        if (display is not null)
        {
            return display;
        }

        return option => metadata.SyntheticZero && option.Equals(metadata.Zero) ? "None" : option.ToString();
    }

    /// <summary>Tests an option as selected: flag membership when multiple, else an exact match unless mixed.</summary>
    static Func<Enum, bool> EnumSelected(bool multiple, Enum shown, bool mixed)
    {
        return option => multiple ? EnumSelection.Contains(shown, option) : !mixed && shown.Equals(option);
    }

    /// <summary>Raises the change signal in the render pass when this frame's build delivered an edit.</summary>
    static void RaiseEnumChanged(Gui gui, ControlNode node, EnumChoiceState state)
    {
        if (gui.Pass == Pass.Pass2Render && state.Choices.ChangedInBuild)
        {
            node.Raise(NodeSignals.Changed);
        }
    }

    static void PrepareEnumValue(Gui gui, EnumChoiceState state, Enum value,
        IReadOnlyList<SelectionChange<Enum>> changes, bool multiple)
    {
        if (gui.Pass != Pass.Pass1Build)
        {
            return;
        }

        state.FrameValue = value;
        foreach (var change in changes)
        {
            state.FrameValue = multiple ? EnumSelection.Apply(state.FrameValue, change) : change.Item;
        }
    }

    static ControlNode RenderEnumChoice(Gui gui, string id, ChoiceState<Enum> state, Enum[] values, Enum current,
        EnumPresentation presentation, Func<Enum, Content> display, Func<Enum, bool> selected,
        Func<Enum, bool>? isMixed, bool multiple, bool mixed, int maxVisibleItems, bool enabled,
        IReadOnlyList<string>? classes)
    {
        if (presentation == EnumPresentation.ToggleButtons)
        {
            return EnumButtons(gui, id, state, values, display, selected, isMixed, multiple, enabled, classes);
        }

        if (presentation == EnumPresentation.Paging)
        {
            return EnumPager(gui, id, state, values, current, display, mixed, maxVisibleItems, enabled, classes);
        }

        return DrawChoices(gui, id, state, values, display, selected, isMixed,
            EqualityComparer<Enum>.Default, mixed ? "—" : display(current).Label ?? "", multiple, false,
            maxVisibleItems, enabled, classes);
    }

    /// <summary>A styled row holding the parts of a segmented or paged enum editor, the scope's field size by default.</summary>
    static ControlNode EnumRow(Gui gui, string type, string id, bool enabled, IReadOnlyList<string>? classes)
    {
        ExcaliburStyles.Ensure(gui);
        var row = gui.StyledControl(type, classes, id, disabled: !enabled);
        UseControlFontSize(gui, row);
        SizeUnlessStyled(row, gui.ControlStyle.FieldWidth, gui.ControlStyle.FieldHeight);
        return row;
    }

    static ControlNode EnumButtons(Gui gui, string id, ChoiceState<Enum> state, Enum[] values,
        Func<Enum, Content> display, Func<Enum, bool> selected, Func<Enum, bool>? mixed,
        bool multiple, bool enabled, IReadOnlyList<string>? classes)
    {
        if (gui.Pass == Pass.Pass1Build)
        {
            FilterChoices(state, values, display, selected, mixed, EqualityComparer<Enum>.Default,
                ChoiceRowHeight(gui, state), Math.Max(1, values.Length));
        }

        var row = EnumRow(gui, "enum-buttons", id, enabled, classes);
        using (row.Direction(Axis.Horizontal).Gap(2).Enter())
        {
            for (var i = 0; i < values.Length; i++)
            {
                EnumToggleButton(gui, id, state, values[i], state.Contents[i], state.Checked[i], state.Mixed[i],
                    multiple, enabled);
            }
        }

        return row;
    }

    static void EnumToggleButton(Gui gui, string id, ChoiceState<Enum> state, Enum option,
        Content content, bool checkedValue, bool isMixed, bool multiple, bool enabled)
    {
        ExcaliburStyles.Ensure(gui);
        var modifiers = checkedValue && !isMixed ? CheckedModifier : NoModifiers;
        var button = gui.StyledNode("button", SegmentClass, $"{id}/toggle/{option}", modifiers, disabled: !enabled);
        UseControlFontSize(gui, button);
        using (button.Expand().BlockInput().AlignContentX(0.5f).AlignContentY(0.5f).Enter())
        {
            gui.Content(isMixed ? Content.Row("—", content) : content);
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            gui.RegisterFocusable(enabled, enabled);
            if (ChoicePressed(gui, enabled))
            {
                gui.RequestFocus(FocusReason.Mouse);
                QueueChoice(state, option, !checkedValue || isMixed, multiple);
            }
        }
    }

    static ControlNode EnumPager(Gui gui, string id, ChoiceState<Enum> state, Enum[] values, Enum current,
        Func<Enum, Content> display, bool mixed, int visible, bool enabled, IReadOnlyList<string>? classes)
    {
        var row = EnumRow(gui, "enum-pager", id, enabled, classes);
        var side = ChoiceRowHeight(gui, state);
        using (row.Direction(Axis.Horizontal).Enter())
        {
            EnumPageButton(gui, $"{id}/previous", "‹", -1, state, values, current, side, enabled);
            using (gui.Node(-1, -1, $"{id}/center").ExpandWidth().ExpandHeight().Enter())
            {
                DrawChoices(gui, $"{id}/center", state, values, display, current.Equals, null,
                    EqualityComparer<Enum>.Default, mixed ? "—" : display(current).Label ?? "", false, false,
                    visible, enabled, null).ExpandWidth().ExpandHeight();
            }

            EnumPageButton(gui, $"{id}/next", "›", 1, state, values, current, side, enabled);
        }

        return row;
    }

    static void EnumPageButton(Gui gui, string id, string label, int step, ChoiceState<Enum> state,
        Enum[] values, Enum current, float side, bool enabled)
    {
        ExcaliburStyles.Ensure(gui);
        using (gui.StyledNode("button", IconClass, id, disabled: !enabled).Width(side).Height(side).BlockInput()
                   .AlignContentX(0.5f).AlignContentY(0.5f).Enter())
        {
            gui.DrawText(label);
            if (gui.Pass != Pass.Pass2Render)
            {
                return;
            }

            gui.RegisterFocusable(enabled && values.Length > 0, enabled);
            if (!enabled || values.Length == 0)
            {
                return;
            }

            if (!ChoicePressed(gui, enabled))
            {
                return;
            }

            gui.RequestFocus(FocusReason.Mouse);
            var next = NextEnumIndex(values, current, step);
            QueueChoice(state, values[next], true, false);
        }
    }

    static int NextEnumIndex(Enum[] values, Enum current, int step)
    {
        var index = Array.IndexOf(values, current);
        if (index < 0)
        {
            return step > 0 ? 0 : values.Length - 1;
        }

        return (index + step + values.Length) % values.Length;
    }
}

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
    static readonly ConditionalWeakTable<Type, EnumChoices> EnumChoicesByType = new();

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
    /// <remarks>Changes arrive in the build pass. Paging cycles declared values and wraps at either end.</remarks>
    public static bool EnumDropdown<T>(this Gui gui, ref T value,
        EnumPresentation presentation = EnumPresentation.Dropdown, Func<T, string>? display = null,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight,
        float fontSize = ControlMetrics.FontSize, int maxVisibleItems = 6, bool enabled = true, bool mixed = false,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) where T : struct, Enum
    {
        var next = gui.EnumDropdown(value, out var changes, presentation,
            display is null ? null : option => display((T)(object)option),
            width, height, fontSize, maxVisibleItems, enabled, mixed, filePath: filePath, lineNumber: lineNumber);
        if (changes.Count == 0) return false;
        value = (T)(object)next;
        return true;
    }

    /// <summary>Draws an enum editor whose individual changes can be applied to every owner of a mixed field.</summary>
    public static Enum EnumDropdown(this Gui gui, Enum value, out IReadOnlyList<SelectionChange<Enum>> changes,
        EnumPresentation presentation = EnumPresentation.Dropdown, Func<Enum, string>? display = null,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight,
        float fontSize = ControlMetrics.FontSize, int maxVisibleItems = 6, bool enabled = true, bool mixed = false,
        Func<Enum, bool>? isMixed = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(value);
        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new EnumChoiceState());
        changes = BeginChoices(gui, state.Choices, enabled);
        var metadata = EnumChoicesByType.GetValue(value.GetType(), static type => new EnumChoices(type));
        var multiple = metadata.Flags && presentation != EnumPresentation.Paging;
        PrepareEnumValue(gui, state, value, changes, multiple);
        var current = state.FrameValue ?? value;
        display ??= option => metadata.SyntheticZero && option.Equals(metadata.Zero) ? "None" : option.ToString();
        Func<Enum, bool> selected = option => multiple
            ? EnumSelection.Contains(current, option) : !mixed && current.Equals(option);
        var options = presentation == EnumPresentation.Paging ? metadata.Values : metadata.SelectionValues;
        RenderEnumChoice(gui, id, state.Choices, options, current, presentation, display, selected, isMixed,
            multiple, mixed, width, height, fontSize, maxVisibleItems, enabled);
        return current;
    }

    static void PrepareEnumValue(Gui gui, EnumChoiceState state, Enum value,
        IReadOnlyList<SelectionChange<Enum>> changes, bool multiple)
    {
        if (gui.Pass != Pass.Pass1Build) return;
        state.FrameValue = value;
        foreach (var change in changes)
            state.FrameValue = multiple ? EnumSelection.Apply(state.FrameValue, change) : change.Item;
    }

    static void RenderEnumChoice(Gui gui, string id, ChoiceState<Enum> state, Enum[] values, Enum current,
        EnumPresentation presentation, Func<Enum, string> display, Func<Enum, bool> selected,
        Func<Enum, bool>? isMixed, bool multiple, bool mixed, float width, float height, float fontSize,
        int maxVisibleItems, bool enabled)
    {
        if (presentation == EnumPresentation.ToggleButtons)
            EnumButtons(gui, id, state, values, display, selected, isMixed,
                multiple, width, height, fontSize, enabled);
        else if (presentation == EnumPresentation.Paging)
            EnumPager(gui, id, state, values, current, display, mixed,
                width, height, fontSize, maxVisibleItems, enabled);
        else
            DrawChoices(gui, id, state, values, display, selected, isMixed,
                EqualityComparer<Enum>.Default, mixed ? "—" : display(current), multiple, false,
                width, height, fontSize, maxVisibleItems, enabled);
    }

    static void EnumButtons(Gui gui, string id, ChoiceState<Enum> state, Enum[] values,
        Func<Enum, string> display, Func<Enum, bool> selected, Func<Enum, bool>? mixed,
        bool multiple, float width, float height, float fontSize, bool enabled)
    {
        if (gui.Pass == Pass.Pass1Build)
            FilterChoices(state, values, display, selected, mixed, EqualityComparer<Enum>.Default,
                height, Math.Max(1, values.Length));
        using (gui.Node(gui.ControlStyle.FieldWidthOr(width), gui.ControlStyle.FieldHeightOr(height), id)
                   .Direction(Axis.Horizontal).Gap(2).Enter())
        {
            for (var i = 0; i < values.Length; i++)
                EnumToggleButton(gui, id, state, values[i], state.Labels[i], state.Checked[i], state.Mixed[i],
                    multiple, fontSize, enabled);
        }
    }

    static void EnumToggleButton(Gui gui, string id, ChoiceState<Enum> state, Enum option,
        string label, bool checkedValue, bool isMixed,
        bool multiple, float fontSize, bool enabled)
    {
        using (gui.Node(-1, -1, $"{id}/toggle/{option}").Expand().BlockInput()
                           .ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            gui.DrawText((isMixed ? "— " : "") + label, gui.ControlStyle.FontSizeOr(fontSize),
                enabled ? gui.ControlStyle.Text : gui.ControlStyle.TextDisabled);
            if (gui.Pass != Pass.Pass2Render) return;
            gui.RegisterFocusable(enabled, enabled);
            gui.DrawBackgroundRect(checkedValue && !isMixed ? gui.ControlStyle.Selected : gui.ControlStyle.Surface, 3);
            if (ChoicePressed(gui, enabled))
            {
                gui.RequestFocus(FocusReason.Mouse);
                QueueChoice(state, option, !checkedValue || isMixed, multiple);
            }
        }
    }

    static void EnumPager(Gui gui, string id, ChoiceState<Enum> state, Enum[] values, Enum current,
        Func<Enum, string> display, bool mixed, float width, float height, float fontSize, int visible, bool enabled)
    {
        using (gui.Node(gui.ControlStyle.FieldWidthOr(width), gui.ControlStyle.FieldHeightOr(height), id)
                   .Direction(Axis.Horizontal).Enter())
        {
            EnumPageButton(gui, $"{id}/previous", "‹", -1, state, values, current, height, fontSize, enabled);
            using (gui.Node(-1, height, $"{id}/center").ExpandWidth().Enter())
                DrawChoices(gui, $"{id}/center", state, values, display, current.Equals, null,
                    EqualityComparer<Enum>.Default, mixed ? "—" : display(current), false, false,
                    0, height, fontSize, visible, enabled);
            EnumPageButton(gui, $"{id}/next", "›", 1, state, values, current, height, fontSize, enabled);
        }
    }

    static void EnumPageButton(Gui gui, string id, string label, int step, ChoiceState<Enum> state,
        Enum[] values, Enum current, float height, float fontSize, bool enabled)
    {
        using (gui.Node(height, height, id).BlockInput().ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            gui.DrawText(label, fontSize, enabled ? gui.ControlStyle.Text : gui.ControlStyle.TextDisabled);
            if (gui.Pass != Pass.Pass2Render) return;
            gui.RegisterFocusable(enabled && values.Length > 0, enabled);
            if (!enabled || values.Length == 0) return;
            if (!ChoicePressed(gui, enabled)) return;
            gui.RequestFocus(FocusReason.Mouse);
            var next = NextEnumIndex(values, current, step);
            QueueChoice(state, values[next], true, false);
        }
    }

    static int NextEnumIndex(Enum[] values, Enum current, int step)
    {
        var index = Array.IndexOf(values, current);
        if (index < 0) return step > 0 ? 0 : values.Length - 1;
        return (index + step + values.Length) % values.Length;
    }
}

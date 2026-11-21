using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Draws a searchable, virtualized checkbox popup that stays open while selecting.</summary>
    /// <remarks>
    /// Equality uses the supplied comparer. Duplicate options share selection and show only their first occurrence.
    /// Select all and Clear affect filtered options; hidden and unknown selections are preserved.
    /// Apply <c>result.Selected</c> when <c>result.Changed</c> is true, and call from the same site in both passes.
    /// The trigger is the scope's field size unless a sheet or the returned node says otherwise; the popup matches
    /// its width and rows are its height.
    /// </remarks>
    /// <param name="gui">The GUI context.</param>
    /// <param name="options">The choices.</param>
    /// <param name="selected">The current selection.</param>
    /// <param name="result">The controlled selection and the edits delivered in the build pass.</param>
    /// <param name="display">What an option shows: text, an icon, widgets; its label is searched and summarized.</param>
    /// <param name="comparer">How options are compared; the default comparer when null.</param>
    /// <param name="maxVisibleItems">How many rows the popup shows before it scrolls.</param>
    /// <param name="placeholder">Shown while nothing is selected.</param>
    /// <param name="chips">Whether the trigger shows removable chips instead of a summary.</param>
    /// <param name="enabled">Whether the dropdown may be opened.</param>
    /// <param name="mixed">Shows a dash until the user edits a selection that differs between owners.</param>
    /// <param name="isMixed">Whether an option is selected in some owners but not all.</param>
    /// <param name="classes">Extra classes for the trigger.</param>
    /// <param name="id">Optional id seeding the identity of the trigger and popup; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The trigger's node, <see cref="LayoutNode.Changed"/> (and as a <c>bool</c>) while the selection changes.</returns>
    public static ControlNode MultiDropdown<T>(this Gui gui, IReadOnlyList<T> options,
        IReadOnlyCollection<T> selected, out MultiDropdownResult<T> result, Func<T, Content>? display = null,
        IEqualityComparer<T>? comparer = null, int maxVisibleItems = 6, string placeholder = "Select options…",
        bool chips = false, bool enabled = true, bool mixed = false, Func<T, bool>? isMixed = null,
        IReadOnlyList<string>? classes = null, string? id = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selected);
        var key = id ?? gui.AutomaticId(filePath, lineNumber);
        var state = gui.ControlState(key, static () => new ChoiceState<T>());
        var changes = BeginChoices(gui, state, enabled);
        comparer ??= EqualityComparer<T>.Default;
        display = DisplayOrItemText(display);

        if (gui.Pass == Pass.Pass1Build)
        {
            state.Selection = ApplyChoices(options, selected, changes, comparer);
        }

        var membership = new HashSet<T>(state.Selection, comparer);
        var trigger = DrawChoices(gui, key, state, options, display, membership.Contains, isMixed, comparer,
            ChoiceSummary(state.Selection, display, placeholder, mixed), true, chips, maxVisibleItems, enabled,
            classes);
        RaiseChoiceChanged(gui, trigger, state);
        result = new MultiDropdownResult<T>(state.Selection, changes);
        return trigger;
    }

    /// <summary>The caller's display, else an option's text, which is empty for a null option.</summary>
    static Func<T, Content> DisplayOrItemText<T>(Func<T, Content>? display)
    {
        if (display is not null)
        {
            return display;
        }

        return static item => item?.ToString() ?? string.Empty;
    }

    /// <summary>Raises the change signal on the trigger when this frame's build edited the selection.</summary>
    static void RaiseChoiceChanged<T>(Gui gui, ControlNode trigger, ChoiceState<T> state)
    {
        if (gui.Pass == Pass.Pass2Render && state.ChangedInBuild)
        {
            trigger.Raise(NodeSignals.Changed);
        }
    }

    static IReadOnlyList<T> ApplyChoices<T>(IReadOnlyList<T> options, IEnumerable<T> selected,
        IReadOnlyList<SelectionChange<T>> changes, IEqualityComparer<T> comparer)
    {
        var membership = new HashSet<T>(selected, comparer);
        foreach (var change in changes)
        {
            if (change.Selected)
            {
                membership.Add(change.Item);
            }
            else
            {
                membership.Remove(change.Item);
            }
        }
        var ordered = new List<T>(membership.Count);
        foreach (var item in options)
        {
            if (membership.Remove(item))
            {
                ordered.Add(item);
            }
        }

        foreach (var item in selected)
        {
            if (membership.Remove(item))
            {
                ordered.Add(item);
            }
        }

        return ordered.AsReadOnly();
    }

    static string ChoiceSummary<T>(IReadOnlyList<T> selected, Func<T, Content> display,
        string placeholder, bool mixed)
    {
        if (mixed)
        {
            return "—";
        }

        if (selected.Count == 0)
        {
            return placeholder;
        }

        return selected.Count <= 2
            ? string.Join(", ", selected.Select(item => display(item).Label ?? ""))
            : $"{selected.Count} selected";
    }

    sealed class ChoiceState<T>
    {
        internal bool Open;
        internal bool? RequestedOpen;
        internal bool JustOpened;

        /// <summary>This frame's build pass delivered selection edits; the render pass reads it to report the change.</summary>
        internal bool ChangedInBuild;
        internal Rect ButtonRect;
        internal Rect Anchor;
        internal Rect PopupRect;
        internal string Query = "";
        internal string FrameQuery = "";
        internal string FrameSummary = "";
        internal float ScrollY;
        internal int First;
        internal int Last;
        internal int Active;
        internal bool Reveal;
        internal readonly List<int> Filtered = [];
        internal readonly List<SelectionChange<T>> Pending = [];
        internal IReadOnlyList<T> Selection = [];
        internal string[] Labels = [];
        internal Content[] Contents = [];
        internal bool[] Checked = [];
        internal bool[] Mixed = [];
    }

    static IReadOnlyList<SelectionChange<T>> BeginChoices<T>(Gui gui, ChoiceState<T> state, bool enabled)
    {
        if (gui.Pass != Pass.Pass1Build)
        {
            return [];
        }

        state.Anchor = state.ButtonRect;
        state.JustOpened = state.RequestedOpen == true && !state.Open;
        if (state.RequestedOpen is { } open)
        {
            state.Open = open;
        }

        state.RequestedOpen = null;
        if (!enabled)
        {
            state.Open = false;
        }

        if (state.JustOpened)
        {
            state.Query = "";
            state.ScrollY = 0;
            state.Active = 0;
        }
        var changes = enabled ? state.Pending.ToArray() : [];
        state.Pending.Clear();
        state.ChangedInBuild = changes.Length > 0;
        return Array.AsReadOnly(changes);
    }

    /// <summary>The height of a trigger and of the popup's rows: the trigger as last laid out, else the field height.</summary>
    static float ChoiceRowHeight<T>(Gui gui, ChoiceState<T> state) =>
        Math.Max(16, state.Anchor.H > 0 ? state.Anchor.H : gui.ControlStyle.FieldHeight);

    static ControlNode DrawChoices<T>(Gui gui, string id, ChoiceState<T> state, IReadOnlyList<T> options,
        Func<T, Content> display, Func<T, bool> selected, Func<T, bool>? mixed, IEqualityComparer<T> comparer,
        string summary, bool multiple, bool chips, int maxVisibleItems, bool enabled,
        IReadOnlyList<string>? classes)
    {
        var height = ChoiceRowHeight(gui, state);
        maxVisibleItems = Math.Max(1, maxVisibleItems);
        if (gui.Pass == Pass.Pass1Build)
        {
            FilterChoices(state, options, display, selected, mixed, comparer, height, maxVisibleItems);
            state.FrameSummary = summary;
        }

        var trigger = ChoiceTrigger(gui, id, state, state.FrameSummary, chips, display, enabled, classes);
        if (state.Open && state.Anchor.W > 0)
        {
            ChoicePopup(gui, id, state, options, multiple, height, maxVisibleItems);
        }

        return trigger;
    }

    static void FilterChoices<T>(ChoiceState<T> state, IReadOnlyList<T> options, Func<T, Content> display,
        Func<T, bool> selected, Func<T, bool>? mixed, IEqualityComparer<T> comparer, float height, int visible)
    {
        if (state.Labels.Length != options.Count)
        {
            state.Labels = new string[options.Count];
            state.Contents = new Content[options.Count];
            state.Checked = new bool[options.Count];
            state.Mixed = new bool[options.Count];
        }
        var queryChanged = state.FrameQuery != state.Query;
        state.FrameQuery = state.Query;
        state.Filtered.Clear();
        var unique = new HashSet<T>(comparer);
        for (var i = 0; i < options.Count; i++)
        {
            state.Contents[i] = display(options[i]);
            state.Labels[i] = state.Contents[i].Label ?? "";
            state.Checked[i] = selected(options[i]);
            state.Mixed[i] = mixed?.Invoke(options[i]) == true;
            if (unique.Add(options[i])
                && state.Labels[i].Contains(state.FrameQuery, StringComparison.OrdinalIgnoreCase))
            {
                state.Filtered.Add(i);
            }
        }
        state.Active = Math.Clamp(state.Active, 0, Math.Max(0, state.Filtered.Count - 1));
        if (queryChanged) { state.ScrollY = 0; state.Active = 0; state.Reveal = true; }
        state.ScrollY = Math.Clamp(state.ScrollY, 0, Math.Max(0, (state.Filtered.Count - visible) * height));
        state.First = Math.Max(0, (int)(state.ScrollY / height) - 2);
        state.Last = Math.Min(state.Filtered.Count, state.First + visible + 5);
    }

    static ControlNode ChoiceTrigger<T>(Gui gui, string id, ChoiceState<T> state, string summary, bool chips,
        Func<T, Content> display, bool enabled, IReadOnlyList<string>? classes)
    {
        ExcaliburStyles.Ensure(gui);
        var modifiers = state.Open && enabled ? OpenModifier : NoModifiers;
        var trigger = gui.StyledControl("dropdown", classes, $"{id}/button", modifiers, disabled: !enabled);
        UseControlFontSize(gui, trigger);
        SizeUnlessStyled(trigger, gui.ControlStyle.FieldWidth, gui.ControlStyle.FieldHeight);
        var padding = gui.ControlStyle.Spacing;
        PadUnlessStyled(trigger, padding, 0);
        using (trigger.Direction(Axis.Horizontal).AlignContentY(0.5f).Enter())
        {
            gui.ClipContent();
            if (gui.Pass == Pass.Pass2Render)
            {
                state.ButtonRect = gui.CurrentNode.Rect;
                gui.RegisterFocusable(enabled, enabled, claimsArrowKeys: true);
                DrawArrow(gui, gui.CurrentNode.Rect, padding);
                if (ChoicePressed(gui, enabled))
                {
                    gui.RequestFocus(gui.Input.IsMouseButtonPressed(MouseButton.Left)
                        ? FocusReason.Mouse : FocusReason.Keyboard);
                    state.RequestedOpen = !state.Open;
                }
            }
            if (chips && state.Selection.Count > 0)
            {
                ChoiceChips(gui, id, state, display, enabled);
            }
            else
            {
                gui.DrawText(summary, centerInRect: false);
            }
        }

        return trigger;
    }

    static void ChoiceChips<T>(Gui gui, string id, ChoiceState<T> state, Func<T, Content> display, bool enabled)
    {
        var count = Math.Min(2, state.Selection.Count);
        var chipHeight = ChoiceRowHeight(gui, state) - 6;
        for (var i = 0; i < count; i++)
        {
            using (gui.StyledNode("chip", id: $"{id}/chip/{i}", disabled: !enabled).Height(chipHeight).Padding(4, 0)
                       .BlockInput().AlignContentY(0.5f).Enter())
            {
                gui.DrawText((display(state.Selection[i]).Label ?? "") + " ×", centerInRect: false);
                if (gui.Pass != Pass.Pass2Render)
                {
                    continue;
                }

                gui.RegisterFocusable(enabled, enabled);
                if (ChoicePressed(gui, enabled))
                {
                    state.Pending.Add(new SelectionChange<T>(state.Selection[i], false));
                }
            }
        }
        if (state.Selection.Count > count)
        {
            gui.DrawText($"+{state.Selection.Count - count}", color: PlaceholderColor(gui, enabled));
        }
    }

    static bool ChoiceActivateKey(Gui gui) =>
        gui.Input.IsKeyPressed(KeyboardKey.Enter) || gui.Input.IsKeyPressed(KeyboardKey.Space);

    static bool ChoicePressed(Gui gui, bool enabled) =>
        enabled && (gui.GetInteractable().OnClick() || gui.HasFocus() && ChoiceActivateKey(gui));
}

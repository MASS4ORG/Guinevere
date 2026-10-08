using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>Draws a searchable, virtualized checkbox popup that stays open while selecting.</summary>
    /// <remarks>
    /// Equality uses the supplied comparer. Duplicate options share selection and show only their first occurrence.
    /// Select all and Clear affect filtered options; hidden and unknown selections are preserved.
    /// Apply the returned selection when Changed is true, and call from the same site in both passes.
    /// </remarks>
    public static MultiDropdownResult<T> MultiDropdown<T>(this Gui gui, IReadOnlyList<T> options,
        IReadOnlyCollection<T> selected, Func<T, string>? display = null, IEqualityComparer<T>? comparer = null,
        float width = ControlMetrics.FieldWidth, float height = ControlMetrics.FieldHeight,
        float fontSize = ControlMetrics.FontSize, int maxVisibleItems = 6, string placeholder = "Select options…",
        bool chips = false, bool enabled = true, bool mixed = false, Func<T, bool>? isMixed = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selected);
        var id = gui.NodeId(filePath, lineNumber);
        var state = gui.ControlState(id, () => new ChoiceState<T>());
        var changes = BeginChoices(gui, state, enabled);
        comparer ??= EqualityComparer<T>.Default;
        display ??= static item => item?.ToString() ?? string.Empty;

        if (gui.Pass == Pass.Pass1Build)
            state.Selection = ApplyChoices(options, selected, changes, comparer);
        var membership = new HashSet<T>(state.Selection, comparer);
        DrawChoices(gui, id, state, options, display, membership.Contains, isMixed, comparer,
            ChoiceSummary(state.Selection, display, placeholder, mixed), true, chips,
            width, height, fontSize, maxVisibleItems, enabled);
        return new MultiDropdownResult<T>(state.Selection, changes);
    }

    static IReadOnlyList<T> ApplyChoices<T>(IReadOnlyList<T> options, IEnumerable<T> selected,
        IReadOnlyList<SelectionChange<T>> changes, IEqualityComparer<T> comparer)
    {
        var membership = new HashSet<T>(selected, comparer);
        foreach (var change in changes)
        {
            if (change.Selected) membership.Add(change.Item);
            else membership.Remove(change.Item);
        }
        var ordered = new List<T>(membership.Count);
        foreach (var item in options)
            if (membership.Remove(item)) ordered.Add(item);
        foreach (var item in selected)
            if (membership.Remove(item)) ordered.Add(item);
        return ordered.AsReadOnly();
    }

    static string ChoiceSummary<T>(IReadOnlyList<T> selected, Func<T, string> display,
        string placeholder, bool mixed)
    {
        if (mixed) return "—";
        if (selected.Count == 0) return placeholder;
        return selected.Count <= 2 ? string.Join(", ", selected.Select(display)) : $"{selected.Count} selected";
    }

    sealed class ChoiceState<T>
    {
        internal bool Open;
        internal bool? RequestedOpen;
        internal bool JustOpened;
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
        internal bool[] Checked = [];
        internal bool[] Mixed = [];
    }

    static IReadOnlyList<SelectionChange<T>> BeginChoices<T>(Gui gui, ChoiceState<T> state, bool enabled)
    {
        if (gui.Pass != Pass.Pass1Build) return [];
        state.Anchor = state.ButtonRect;
        state.JustOpened = state.RequestedOpen == true && !state.Open;
        if (state.RequestedOpen is { } open) state.Open = open;
        state.RequestedOpen = null;
        if (!enabled) state.Open = false;
        if (state.JustOpened)
        {
            state.Query = "";
            state.ScrollY = 0;
            state.Active = 0;
        }
        var changes = enabled ? state.Pending.ToArray() : [];
        state.Pending.Clear();
        return Array.AsReadOnly(changes);
    }

    static void DrawChoices<T>(Gui gui, string id, ChoiceState<T> state, IReadOnlyList<T> options,
        Func<T, string> display, Func<T, bool> selected, Func<T, bool>? mixed, IEqualityComparer<T> comparer,
        string summary, bool multiple, bool chips, float width, float height, float fontSize,
        int maxVisibleItems, bool enabled)
    {
        height = Math.Max(16, gui.ControlStyle.FieldHeightOr(height));
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        maxVisibleItems = Math.Max(1, maxVisibleItems);
        if (gui.Pass == Pass.Pass1Build)
        {
            FilterChoices(state, options, display, selected, mixed, comparer, height, maxVisibleItems);
            state.FrameSummary = summary;
        }
        ChoiceTrigger(gui, id, state, state.FrameSummary, chips, display, width, height, fontSize, enabled);
        if (state.Open && state.Anchor.W > 0)
            ChoicePopup(gui, id, state, options, multiple, height, fontSize, maxVisibleItems);
    }

    static void FilterChoices<T>(ChoiceState<T> state, IReadOnlyList<T> options, Func<T, string> display,
        Func<T, bool> selected, Func<T, bool>? mixed, IEqualityComparer<T> comparer, float height, int visible)
    {
        if (state.Labels.Length != options.Count)
        {
            state.Labels = new string[options.Count];
            state.Checked = new bool[options.Count];
            state.Mixed = new bool[options.Count];
        }
        var queryChanged = state.FrameQuery != state.Query;
        state.FrameQuery = state.Query;
        state.Filtered.Clear();
        var unique = new HashSet<T>(comparer);
        for (var i = 0; i < options.Count; i++)
        {
            state.Labels[i] = display(options[i]);
            state.Checked[i] = selected(options[i]);
            state.Mixed[i] = mixed?.Invoke(options[i]) == true;
            if (unique.Add(options[i])
                && state.Labels[i].Contains(state.FrameQuery, StringComparison.OrdinalIgnoreCase))
                state.Filtered.Add(i);
        }
        state.Active = Math.Clamp(state.Active, 0, Math.Max(0, state.Filtered.Count - 1));
        if (queryChanged) { state.ScrollY = 0; state.Active = 0; state.Reveal = true; }
        state.ScrollY = Math.Clamp(state.ScrollY, 0, Math.Max(0, (state.Filtered.Count - visible) * height));
        state.First = Math.Max(0, (int)(state.ScrollY / height) - 2);
        state.Last = Math.Min(state.Filtered.Count, state.First + visible + 5);
    }

    static void ChoiceTrigger<T>(Gui gui, string id, ChoiceState<T> state, string summary, bool chips,
        Func<T, string> display, float width, float height, float fontSize, bool enabled)
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        ExcaliburStyles.Ensure(gui);
        var modifiers = state.Open && enabled ? OpenModifier : NoModifiers;
        using (Sized(gui.StyledNode("dropdown", id: $"{id}/button", modifiers: modifiers, disabled: !enabled),
                       width, height)
                   .Direction(Axis.Horizontal).Padding(8, 0).ContentAlignY(0.5f).Enter())
        {
            gui.ClipContent();
            if (gui.Pass == Pass.Pass2Render)
            {
                state.ButtonRect = gui.CurrentNode.Rect;
                gui.RegisterFocusable(enabled, enabled, claimsArrowKeys: true);
                DrawArrow(gui, gui.CurrentNode.Rect, 8);
                if (ChoicePressed(gui, enabled))
                {
                    gui.RequestFocus(gui.Input.IsMouseButtonPressed(MouseButton.Left)
                        ? FocusReason.Mouse : FocusReason.Keyboard);
                    state.RequestedOpen = !state.Open;
                }
            }
            if (chips && state.Selection.Count > 0)
                ChoiceChips(gui, id, state, display, height, fontSize, enabled);
            else gui.DrawText(summary, fontSize, centerInRect: false);
        }
    }

    static void ChoiceChips<T>(Gui gui, string id, ChoiceState<T> state, Func<T, string> display,
        float height, float fontSize, bool enabled)
    {
        var count = Math.Min(2, state.Selection.Count);
        for (var i = 0; i < count; i++)
        {
            using (gui.StyledNode("chip", id: $"{id}/chip/{i}", disabled: !enabled).Height(height - 6).Padding(4, 0)
                       .BlockInput().ContentAlignY(0.5f).Enter())
            {
                gui.DrawText(display(state.Selection[i]) + " ×", fontSize, centerInRect: false);
                if (gui.Pass != Pass.Pass2Render) continue;
                gui.RegisterFocusable(enabled, enabled);
                if (ChoicePressed(gui, enabled))
                    state.Pending.Add(new SelectionChange<T>(state.Selection[i], false));
            }
        }
        if (state.Selection.Count > count)
            gui.DrawText($"+{state.Selection.Count - count}", fontSize, PlaceholderColor(gui, enabled));
    }

    static bool ChoiceActivateKey(Gui gui) =>
        gui.Input.IsKeyPressed(KeyboardKey.Enter) || gui.Input.IsKeyPressed(KeyboardKey.Space);

    static bool ChoicePressed(Gui gui, bool enabled) =>
        enabled && (gui.GetInteractable().OnClick() || gui.HasFocus() && ChoiceActivateKey(gui));
}

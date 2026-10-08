using System.Runtime.CompilerServices;

namespace Guinevere;

public static partial class ControlsExtensions
{
    class DropdownState
    {
        public bool IsOpen { get; set; }
        public int SelectedIndex { get; set; } = -1;

        /// <summary>Open/close input detected during render and committed before the next layout pass.</summary>
        public bool? RequestedOpen { get; set; }

        /// <summary>Option input detected during render and committed before the next layout pass.</summary>
        public int? RequestedSelection { get; set; }

        /// <summary>The button's rect as last measured, which is where the list anchors.</summary>
        public Rect ButtonRect { get; set; }

        /// <summary>
        /// The anchor both passes of the current frame use. A node's rect only resolves in the render
        /// pass, so reading it directly would place the list differently in each pass.
        /// </summary>
        public Rect Anchor { get; set; }
    }

    static readonly string[] OpenModifier = [StyleModifiers.Open];
    static readonly string[] SelectedModifier = [StyleModifiers.Selected];

    /// <summary>
    /// A dropdown that opens a list of options over the rest of the frame. The button is styled by the
    /// <c>dropdown</c> rules (<c>:open</c>, <c>:hover</c>, <c>:disabled</c>), with its label in a <c>value</c> or
    /// <c>placeholder</c> child; the list is a <c>listbox</c> of <c>option</c> rows, the chosen one <c>:selected</c>.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="options">The choices.</param>
    /// <param name="selectedIndex">The chosen index, updated on selection. Negative shows the placeholder.</param>
    /// <param name="width">Button width. Zero fills the parent.</param>
    /// <param name="height">Button height.</param>
    /// <param name="placeholder">Shown when nothing is selected.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="padding">Horizontal padding inside the button and the options.</param>
    /// <param name="maxVisibleItems">How many options the list shows before it scrolls.</param>
    /// <param name="enabled">Whether the dropdown may be opened. A disabled dropdown is dimmed and inert.</param>
    /// <param name="classes">Extra classes for the button.</param>
    /// <param name="filePath">Call site, supplied by the compiler. Pass an id to separate two dropdowns sharing one.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    public static void Dropdown(this Gui gui, string[] options, ref int selectedIndex,
        float width = ControlMetrics.FieldWidth,
        float height = ControlMetrics.FieldHeight,
        string placeholder = "Select an option...",
        float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing,
        int maxVisibleItems = 6,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        width = gui.ControlStyle.FieldWidthOr(width);
        height = gui.ControlStyle.FieldHeightOr(height);
        fontSize = gui.ControlStyle.FontSizeOr(fontSize);
        padding = gui.ControlStyle.SpacingOr(padding);

        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);
        ExcaliburStyles.Ensure(gui);

        var id = gui.NodeId(filePath, lineNumber);
        var state = DropdownStateFor(gui, id);

        if (gui.Pass == Pass.Pass1Build)
        {
            state.Anchor = state.ButtonRect;
            if (state.RequestedOpen is { } requestedOpen)
            {
                state.IsOpen = requestedOpen;
                state.RequestedOpen = null;
            }

            if (state.RequestedSelection is { } requestedSelection)
            {
                selectedIndex = requestedSelection;
                state.SelectedIndex = requestedSelection;
                state.RequestedSelection = null;
            }

            if (!enabled) state.IsOpen = false;
        }

        DrawButton(gui, id, options, selectedIndex, width, height, placeholder, fontSize, padding, state, enabled,
            classes);

        if (!enabled || !state.IsOpen || state.Anchor.W <= 0) return;

        DrawList(gui, id, options, ref selectedIndex, state.Anchor, height, fontSize, padding, maxVisibleItems, state);
    }

    /// <summary>
    /// A dropdown that returns the chosen index instead of taking it by reference.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="options">The choices.</param>
    /// <param name="selectedIndex">The currently chosen index.</param>
    /// <param name="width">Button width. Zero fills the parent.</param>
    /// <param name="height">Button height.</param>
    /// <param name="placeholder">Shown when nothing is selected.</param>
    /// <param name="fontSize">Label size.</param>
    /// <param name="padding">Horizontal padding.</param>
    /// <param name="maxVisibleItems">How many options the list shows before it scrolls.</param>
    /// <param name="enabled">Whether the dropdown may be opened. A disabled dropdown is dimmed and inert.</param>
    /// <param name="classes">Extra classes for the button.</param>
    /// <param name="filePath">Call site, supplied by the compiler.</param>
    /// <param name="lineNumber">Call site, supplied by the compiler.</param>
    /// <returns>The chosen index after this frame.</returns>
    public static int Dropdown(this Gui gui, string[] options, int selectedIndex = -1,
        float width = ControlMetrics.FieldWidth,
        float height = ControlMetrics.FieldHeight,
        string placeholder = "Select an option...",
        float fontSize = ControlMetrics.FontSize,
        float padding = ControlMetrics.Spacing,
        int maxVisibleItems = 6,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        var index = selectedIndex;
        Dropdown(gui, options, ref index, width, height, placeholder, fontSize, padding, maxVisibleItems, enabled,
            classes, filePath, lineNumber);
        return index;
    }

    /// <summary>Closes every open dropdown and forgets their state.</summary>
    /// <param name="gui">The GUI instance.</param>
    public static void ClearDropdownStates(this Gui gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        gui.ClearControlStates<DropdownState>();
    }

    static DropdownState DropdownStateFor(Gui gui, string id) =>
        gui.ControlState(id, () => new DropdownState());

    static void DrawButton(Gui gui, string id, string[] options, int selectedIndex, float width, float height,
        string placeholder, float fontSize, float padding, DropdownState state, bool enabled,
        IReadOnlyList<string>? classes)
    {
        var modifiers = state.IsOpen && enabled ? OpenModifier : NoModifiers;
        using (Sized(gui.StyledNode("dropdown", classes, $"{id}/button", modifiers, disabled: !enabled), width, height)
                   .Direction(Axis.Horizontal).Padding(padding, 0).ContentAlignY(0.5f).Enter())
        {
            if (gui.Pass == Pass.Pass2Render)
            {
                var rect = gui.CurrentNode.Rect;
                state.ButtonRect = rect;
                if (state.Anchor.W <= 0) state.Anchor = rect;

                gui.RegisterFocusable();
                var interactable = gui.GetInteractable();

                if (enabled && interactable.OnClick())
                {
                    gui.RequestFocus(FocusReason.Mouse);
                    state.RequestedOpen = !state.IsOpen;
                }

                DrawArrow(gui, rect, padding);
            }

            var label = selectedIndex >= 0 && selectedIndex < options.Length ? options[selectedIndex] : "";
            DrawInputText(gui, label, placeholder, fontSize, enabled, clip: false);
        }
    }

    /// <summary>The open-list arrow at the right of a dropdown button, in the button's text color.</summary>
    static void DrawArrow(Gui gui, Rect rect, float padding)
    {
        const float size = 4f;
        var color = gui.CurrentNodeScope.Get<LayoutNodeScopeTextColor>().Value;
        var x = rect.X + rect.W - padding - size;
        var y = rect.Y + (rect.H / 2f);

        gui.DrawTriangleFilled(
            new Vector2(x - size, y - (size / 2f)),
            new Vector2(x + size, y - (size / 2f)),
            new Vector2(x, y + (size / 2f)), color);
    }

    /// <summary>
    /// The option list, positioned over the frame rather than inside the flow, so it is not clipped by
    /// whatever panel the dropdown sits in.
    /// </summary>
    static void DrawList(Gui gui, string id, string[] options, ref int selectedIndex, Rect buttonRect,
        float rowHeight, float fontSize, float padding, int maxVisibleItems, DropdownState state)
    {
        var visible = Math.Min(options.Length, Math.Max(1, maxVisibleItems));
        var listHeight = visible * rowHeight;
        var top = buttonRect.Y + buttonRect.H + 2;

        if (top + listHeight > gui.ScreenRect.H) top = Math.Max(0, buttonRect.Y - listHeight - 2);

        var chosen = -1;

        using (gui.StyledNode("listbox", id: $"{id}/list").Width(buttonRect.W).Height(listHeight)
                   .AbsoluteScreen(buttonRect.X, top)
                   .BlockInput()
                   .Direction(Axis.Vertical)
                   .Enter())
        {
            using var focusScope = gui.EnterFocusNavigationScope($"{id}/focus");
            focusScope.SetActive();
            gui.SetZIndex(ListZIndex);
            gui.ScrollY();

            for (var i = 0; i < options.Length; i++)
            {
                var modifiers = i == selectedIndex ? SelectedModifier : NoModifiers;
                using (gui.StyledNode("option", id: $"{id}/list/{i}", modifiers: modifiers).Height(rowHeight)
                           .ExpandWidth().Padding(padding, 0).ContentAlignY(0.5f).Enter())
                {
                    if (DropdownOptionInput(gui, id)) chosen = i;

                    gui.DrawText(options[i], fontSize, centerInRect: false);
                }
            }
        }

        CompleteDropdownSelection(gui, state, chosen, buttonRect);
    }

    static bool DropdownOptionInput(Gui gui, string id)
    {
        if (gui.Pass != Pass.Pass2Render) return false;
        gui.RegisterFocusable(parentId: $"{id}/button");
        var interactable = gui.GetInteractable();
        if (!interactable.OnClick()) return false;
        gui.RequestFocus(FocusReason.Mouse);
        return true;
    }

    static void CompleteDropdownSelection(Gui gui, DropdownState state, int chosen, Rect buttonRect)
    {
        if (gui.Pass != Pass.Pass2Render) return;

        if (chosen >= 0)
        {
            state.RequestedSelection = chosen;
            state.RequestedOpen = false;
            return;
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.Escape)) state.RequestedOpen = false;

        // A press that reached neither the button nor the list dismisses it. The list blocks input, so
        // a press inside it never gets here.
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left)
            && !buttonRect.Contains(gui.Input.MousePosition)
            && !gui.IsPointerOverBlocker)
            state.RequestedOpen = false;
    }

    /// <summary>Where an open option list draws, above ordinary content but below a drag ghost.</summary>
    const int ListZIndex = 5_000;

    static bool IsMouseInRect(Vector2 mousePos, Rect rect) =>
        mousePos.X >= rect.X && mousePos.X <= rect.X + rect.W &&
        mousePos.Y >= rect.Y && mousePos.Y <= rect.Y + rect.H;
}

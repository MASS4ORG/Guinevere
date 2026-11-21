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

        /// <summary>The selection moved when this frame's build pass committed a request; reported in the render pass.</summary>
        public bool ChangedInBuild { get; set; }

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
    /// <remarks>
    /// The button is the scope's field size and spacing unless a sheet or the returned node says otherwise; the list
    /// matches the button's width and rows are its height. <paramref name="id"/> seeds the identity: the button's node
    /// id is <c>{id}/button</c> and the list's <c>{id}/list</c>. The selection is committed in the build pass of the
    /// frame after the click, and <see cref="LayoutNode.Changed"/> reads true in that frame's render pass.
    /// </remarks>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="options">The choices; each is any <see cref="Content"/>, and its label is read by assistive tools.</param>
    /// <param name="selectedIndex">The chosen index, updated on selection. Negative shows the placeholder.</param>
    /// <param name="placeholder">Shown when nothing is selected.</param>
    /// <param name="maxVisibleItems">How many options the list shows before it scrolls.</param>
    /// <param name="enabled">Whether the dropdown may be opened. A disabled dropdown is dimmed and inert.</param>
    /// <param name="classes">Extra classes for the button.</param>
    /// <param name="id">Optional id seeding the identity of the button and list; the call site is the default.</param>
    /// <param name="filePath">Compiler-supplied; do not pass.</param>
    /// <param name="lineNumber">Compiler-supplied; do not pass.</param>
    /// <returns>The button's node, <see cref="LayoutNode.Changed"/> (and as a <c>bool</c>) when the selection moved.</returns>
    public static ControlNode Dropdown(this Gui gui, IReadOnlyList<Content> options, ref int selectedIndex,
        string placeholder = "Select an option...",
        int maxVisibleItems = 6,
        bool enabled = true,
        IReadOnlyList<string>? classes = null,
        string? id = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(options);
        ExcaliburStyles.Ensure(gui);

        var key = id ?? gui.AutomaticId(filePath, lineNumber);
        var state = DropdownStateFor(gui, key);

        ApplyDropdownRequests(gui, state, ref selectedIndex, enabled);

        var button = DrawButton(gui, key, options, selectedIndex, placeholder, state, enabled, classes);
        if (gui.Pass == Pass.Pass2Render && state.ChangedInBuild)
        {
            button.Raise(NodeSignals.Changed);
        }

        if (enabled && state.IsOpen && state.Anchor.W > 0)
        {
            DrawList(gui, key, options, selectedIndex, state.Anchor, maxVisibleItems, state);
        }

        return button;
    }

    static void ApplyDropdownRequests(Gui gui, DropdownState state, ref int selectedIndex, bool enabled)
    {
        if (gui.Pass != Pass.Pass1Build)
        {
            return;
        }

        state.Anchor = state.ButtonRect;
        state.ChangedInBuild = false;
        if (state.RequestedOpen is { } requestedOpen)
        {
            state.IsOpen = requestedOpen;
            state.RequestedOpen = null;
        }
        if (state.RequestedSelection is { } requestedSelection)
        {
            state.ChangedInBuild = requestedSelection != selectedIndex;
            selectedIndex = requestedSelection;
            state.SelectedIndex = requestedSelection;
            state.RequestedSelection = null;
        }
        if (!enabled)
        {
            state.IsOpen = false;
        }
    }

    /// <summary>Closes every open dropdown and forgets their state.</summary>
    /// <param name="gui">The GUI instance.</param>
    public static void ClearDropdownStates(this Gui gui)
    {
        ArgumentNullException.ThrowIfNull(gui);
        gui.ClearControlStates<DropdownState>();
    }

    static DropdownState DropdownStateFor(Gui gui, string id) =>
        gui.ControlState(id, static () => new DropdownState());

    static ControlNode DrawButton(Gui gui, string id, IReadOnlyList<Content> options, int selectedIndex,
        string placeholder, DropdownState state, bool enabled, IReadOnlyList<string>? classes)
    {
        var modifiers = state.IsOpen && enabled ? OpenModifier : NoModifiers;
        var button = gui.StyledControl("dropdown", classes, $"{id}/button", modifiers, disabled: !enabled);
        UseControlFontSize(gui, button);
        SizeUnlessStyled(button, gui.ControlStyle.FieldWidth, gui.ControlStyle.FieldHeight);
        var padding = gui.ControlStyle.Spacing;
        PadUnlessStyled(button, padding, 0);
        using (button.Direction(Axis.Horizontal).AlignContentY(0.5f).Enter())
        {
            gui.CurrentNode.On<KeyDownEvent>(e => DropdownKey(gui, e, id, options.Count, selectedIndex, state, enabled));
            var selected = selectedIndex >= 0 && selectedIndex < options.Count ? options[selectedIndex] : default;
            if (gui.Pass == Pass.Pass2Render)
            {
                DropdownButtonInput(gui, state, enabled);
                DrawArrow(gui, gui.CurrentNode.Rect, padding);
                PublishDropdown(gui, placeholder, selected.Label ?? "", enabled, state.IsOpen);
            }

            if (selected.IsEmpty)
            {
                DrawInputText(gui, "", placeholder, FieldFontSize(gui), enabled, clip: false);
            }
            else
            {
                gui.Content(selected);
            }
        }

        return button;
    }

    static void DropdownButtonInput(Gui gui, DropdownState state, bool enabled)
    {
        var rect = gui.CurrentNode.Rect;
        state.ButtonRect = rect;
        if (state.Anchor.W <= 0)
        {
            state.Anchor = rect;
        }
        gui.RegisterFocusable(canReceiveFocus: enabled, claimsArrowKeys: true);
        var interactable = gui.GetInteractable();
        if (enabled && (interactable.OnClick()
                        || gui.HasFocus() && gui.ControlActivation?.IsActivated(gui.CurrentNode.Id) == true))
        {
            gui.RequestFocus(FocusReason.Mouse);
            state.RequestedOpen = !state.IsOpen;
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
    static void DrawList(Gui gui, string id, IReadOnlyList<Content> options, int selectedIndex, Rect buttonRect,
        int maxVisibleItems, DropdownState state)
    {
        var rowHeight = buttonRect.H;
        var padding = gui.ControlStyle.Spacing;
        var visible = Math.Min(options.Count, Math.Max(1, maxVisibleItems));
        var listHeight = visible * rowHeight;
        var top = buttonRect.Y + buttonRect.H + 2;

        if (top + listHeight > gui.LayoutBounds.H)
        {
            top = Math.Max(0, buttonRect.Y - listHeight - 2);
        }

        var chosen = -1;

        using (gui.StyledNode("listbox", id: $"{id}/list").Width(buttonRect.W).Height(listHeight)
                   .AbsoluteScreen(buttonRect.X, top)
                   .BlockInput()
                   .Direction(Axis.Vertical)
                   .Enter())
        {
            using var focusScope = gui.EnterFocusNavigationScope($"{id}/focus");
            focusScope.SetActive();
            gui.SetZIndex(Math.Max(ListZIndex,
                gui.CurrentNode.Parent!.Scope.Get<LayoutNodeScopeZIndex>().Value + 1));
            gui.ScrollY();
            UseControlFontSize(gui, gui.CurrentNode);

            for (var i = 0; i < options.Count; i++)
            {
                var modifiers = i == selectedIndex ? SelectedModifier : NoModifiers;
                using (gui.StyledNode("option", id: $"{id}/list/{i}", modifiers: modifiers).Height(rowHeight)
                           .ExpandWidth().Padding(padding, 0).AlignContentY(0.5f).Enter())
                {
                    var option = i;
                    gui.CurrentNode.On<KeyDownEvent>(e => DropdownOptionKey(gui, e, id, options.Count, option, state));
                    PublishMenuSemantics(gui, options[i].Label ?? "", true, i == selectedIndex);
                    if (DropdownOptionInput(gui, id))
                    {
                        chosen = i;
                    }

                    gui.Content(options[i]);
                }
            }
        }

        CompleteDropdownSelection(gui, state, chosen, buttonRect);
    }

    static bool DropdownOptionInput(Gui gui, string id)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return false;
        }

        gui.RegisterFocusable(parentId: $"{id}/button");
        var interactable = gui.GetInteractable();
        if (!interactable.OnClick() && !(gui.HasFocus() && gui.IsAcceptPressed))
        {
            return false;
        }

        gui.RequestFocus(FocusReason.Mouse);
        return true;
    }

    static void CompleteDropdownSelection(Gui gui, DropdownState state, int chosen, Rect buttonRect)
    {
        if (gui.Pass != Pass.Pass2Render)
        {
            return;
        }

        if (chosen >= 0)
        {
            state.RequestedSelection = chosen;
            state.RequestedOpen = false;
            return;
        }

        if (gui.IsCancelPressed)
        {
            state.RequestedOpen = false;
        }

        // A press that reached neither the button nor the list dismisses it. The list blocks input, so
        // a press inside it never gets here.
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left)
            && !gui.IsPointerIn(buttonRect)
            && !gui.IsPointerOverBlocker)
        {
            state.RequestedOpen = false;
        }
    }

    /// <summary>Where an open option list draws, above ordinary content but below a drag ghost.</summary>
    const int ListZIndex = 5_000;

    static bool IsMouseInRect(Vector2 mousePos, Rect rect) =>
        mousePos.X >= rect.X && mousePos.X <= rect.X + rect.W &&
        mousePos.Y >= rect.Y && mousePos.Y <= rect.Y + rect.H;
}

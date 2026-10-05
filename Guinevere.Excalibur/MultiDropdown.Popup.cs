namespace Guinevere;

public static partial class ControlsExtensions
{
    static void ChoicePopup<T>(Gui gui, string id, ChoiceState<T> state, IReadOnlyList<T> options,
        bool multiple, float rowHeight, float fontSize, int visible)
    {
        var rows = Math.Max(1, Math.Min(visible, state.Filtered.Count));
        var popupHeight = (rows + (multiple ? 2 : 1)) * rowHeight;
        var x = Math.Clamp(state.Anchor.X, gui.ScreenRect.X,
            Math.Max(gui.ScreenRect.X, gui.ScreenRect.X + gui.ScreenRect.W - state.Anchor.W));
        var y = state.Anchor.Y + state.Anchor.H + 2;
        if (y + popupHeight > gui.ScreenRect.Y + gui.ScreenRect.H)
            y = Math.Max(gui.ScreenRect.Y, state.Anchor.Y - popupHeight - 2);
        using (gui.Node(state.Anchor.W, popupHeight, $"{id}/popup").AbsoluteScreen(x, y)
                   .BlockInput().Direction(Axis.Vertical).Enter())
        {
            gui.SetEscapesAncestorClips();
            gui.SetZIndex(ListZIndex);
            using var scope = gui.EnterFocusNavigationScope($"{id}/focus", $"{id}/button");
            scope.SetActive($"{id}/button");
            if (gui.Pass == Pass.Pass2Render)
            {
                state.PopupRect = gui.CurrentNode.Rect;
                gui.DrawBackgroundRect(gui.ControlStyle.Popup, 4);
                gui.DrawRectBorder(gui.CurrentNode.Rect, gui.ControlStyle.Border, 1, 4);
            }
            ChoiceSearch(gui, id, state, rowHeight, fontSize);
            if (multiple) ChoiceBulk(gui, id, state, options, rowHeight, fontSize);
            ChoiceRows(gui, id, state, options, multiple, rows * rowHeight, rowHeight, fontSize);
            if (gui.Pass == Pass.Pass2Render) DismissChoices(gui, id, state);
        }
    }

    static void ChoiceSearch<T>(Gui gui, string id, ChoiceState<T> state, float height, float fontSize)
    {
        using (gui.Node(-1, height, $"{id}/search").ExpandWidth().Enter())
        {
            var query = gui.TextInput(state.FrameQuery, width: 0, height: height, fontSize: fontSize,
                placeholder: "Search…", id: $"{id}/search-text", grabFocus: state.JustOpened);
            if (gui.Pass == Pass.Pass2Render) state.Query = query;
        }
    }

    static void ChoiceBulk<T>(Gui gui, string id, ChoiceState<T> state, IReadOnlyList<T> options,
        float height, float fontSize)
    {
        using (gui.Node(-1, height, $"{id}/actions").ExpandWidth().Direction(Axis.Horizontal).Enter())
        {
            ChoiceBulkButton(gui, $"{id}/all", "Select all", state, options, true, height, fontSize);
            ChoiceBulkButton(gui, $"{id}/clear", "Clear", state, options, false, height, fontSize);
        }
    }

    static void ChoiceBulkButton<T>(Gui gui, string id, string label, ChoiceState<T> state,
        IReadOnlyList<T> options, bool selected, float height, float fontSize)
    {
        using (gui.Node(-1, height, id).ExpandWidth().ContentAlignX(0.5f).ContentAlignY(0.5f).Enter())
        {
            gui.DrawText(label, fontSize, gui.ControlStyle.Text);
            if (gui.Pass != Pass.Pass2Render) return;
            gui.RegisterFocusable(claimsArrowKeys: true);
            if (!gui.GetInteractable().OnClick() && !(gui.HasFocus() && ChoiceActivateKey(gui))) return;
            gui.RequestFocus(FocusReason.Mouse);
            foreach (var index in state.Filtered)
                state.Pending.Add(new SelectionChange<T>(options[index], selected));
        }
    }

    static void ChoiceRows<T>(Gui gui, string id, ChoiceState<T> state, IReadOnlyList<T> options,
        bool multiple, float viewport, float height, float fontSize)
    {
        using (gui.Node(-1, viewport, $"{id}/list").ExpandWidth().Direction(Axis.Vertical).Enter())
        {
            RevealChoice(gui, state);
            gui.ScrollY();
            if (gui.Pass == Pass.Pass2Render) gui.RegisterFocusable(claimsArrowKeys: true);
            ListingSpacer(gui, $"{id}/pad-top", state.First * height);
            for (var i = state.First; i < state.Last; i++)
                ChoiceRow(gui, id, state, options, i, multiple, height, fontSize);
            ListingSpacer(gui, $"{id}/pad-bottom", (state.Filtered.Count - state.Last) * height);
            if (state.Filtered.Count == 0)
                using (gui.Node(-1, height, $"{id}/empty").ExpandWidth().ContentAlignY(0.5f).Enter())
                    gui.DrawText("No results", fontSize, gui.ControlStyle.TextDim);
            if (gui.Pass != Pass.Pass2Render) return;
            state.ScrollY = gui.GetScrollState(gui.CurrentNode.Id)?.ScrollOffset.Y ?? 0;
            NavigateChoices(gui, id, state, options, multiple, viewport, height);
        }
    }

    static void ChoiceRow<T>(Gui gui, string id, ChoiceState<T> state, IReadOnlyList<T> options,
        int position, bool multiple, float height, float fontSize)
    {
        var index = state.Filtered[position];
        using (gui.Node(-1, height, $"{id}/option/{index}").ExpandWidth().Padding(8, 0)
                   .ContentAlignY(0.5f).Enter())
        {
            var indicator = ChoiceIndicator(multiple, state.Mixed[index], state.Checked[index]);
            gui.DrawText(indicator + state.Labels[index], fontSize, gui.ControlStyle.Text, centerInRect: false);
            if (gui.Pass != Pass.Pass2Render) return;
            var interaction = gui.GetInteractable();
            if (state.Checked[index]) gui.DrawBackgroundRect(gui.ControlStyle.Selected, 2);
            else if (interaction.OnHover() || position == state.Active)
                gui.DrawBackgroundRect(gui.ControlStyle.SurfaceHover, 2);
            if (!interaction.OnClick()) return;
            state.Active = position;
            gui.RequestFocus($"{id}/list", FocusReason.Mouse);
            QueueChoice(state, options[index], !state.Checked[index] || state.Mixed[index], multiple);
        }
    }

    static void QueueChoice<T>(ChoiceState<T> state, T item, bool selected, bool multiple)
    {
        state.Pending.Add(new SelectionChange<T>(item, !multiple || selected));
        if (!multiple) state.RequestedOpen = false;
    }

    static void RevealChoice<T>(Gui gui, ChoiceState<T> state)
    {
        if (gui.Pass != Pass.Pass1Build || !state.Reveal) return;
        if (gui.GetScrollState(gui.CurrentNode.Id) is { } scroll)
            scroll.ScrollOffset = new Vector2(0, state.ScrollY);
        state.Reveal = false;
    }

    static string ChoiceIndicator(bool multiple, bool mixed, bool selected)
    {
        if (!multiple) return "";
        if (mixed) return "— ";
        return selected ? "☑ " : "☐ ";
    }

    static void NavigateChoices<T>(Gui gui, string id, ChoiceState<T> state, IReadOnlyList<T> options,
        bool multiple, float viewport, float height)
    {
        if (!ChoiceOwnsFocus(gui, id) || state.Filtered.Count == 0) return;
        var next = ChoiceNavigationIndex(gui, state.Active, state.Filtered.Count, (int)(viewport / height));
        if (next != state.Active)
        {
            state.Active = next;
            var top = next * height;
            state.ScrollY = Math.Clamp(state.ScrollY, Math.Max(0, top + height - viewport), top);
            state.Reveal = true;
            gui.RequestFocus($"{id}/list", FocusReason.Keyboard);
        }
        if (!ChoiceKeyboardActivation(gui, id)) return;
        var index = state.Filtered[state.Active];
        QueueChoice(state, options[index], !state.Checked[index] || state.Mixed[index], multiple);
    }

    static bool ChoiceOwnsFocus(Gui gui, string id) => gui.Focus.ActiveScopeId == $"{id}/focus"
        && gui.Focus.CurrentFocusedId is { } focused && focused.StartsWith(id + "/", StringComparison.Ordinal);

    static bool ChoiceKeyboardActivation(Gui gui, string id) =>
        (gui.Focus.IsTextInputFocused || gui.HasFocus($"{id}/list"))
        && (gui.Input.IsKeyPressed(KeyboardKey.Enter)
            || gui.HasFocus($"{id}/list") && gui.Input.IsKeyPressed(KeyboardKey.Space));

    static int ChoiceNavigationIndex(Gui gui, int current, int count, int page)
    {
        if (gui.Input.IsKeyPressed(KeyboardKey.Down)) return Math.Min(count - 1, current + 1);
        if (gui.Input.IsKeyPressed(KeyboardKey.Up)) return Math.Max(0, current - 1);
        if (gui.Focus.IsTextInputFocused) return current;
        if (gui.Input.IsKeyPressed(KeyboardKey.Home)) return 0;
        if (gui.Input.IsKeyPressed(KeyboardKey.End)) return count - 1;
        if (gui.Input.IsKeyPressed(KeyboardKey.PageDown)) return Math.Min(count - 1, current + page);
        if (gui.Input.IsKeyPressed(KeyboardKey.PageUp)) return Math.Max(0, current - page);
        return current;
    }

    static void DismissChoices<T>(Gui gui, string id, ChoiceState<T> state)
    {
        if (gui.Focus.ActiveScopeId == $"{id}/focus" && gui.Input.IsKeyPressed(KeyboardKey.Escape))
        {
            state.RequestedOpen = false;
            gui.RequestFocus($"{id}/button");
        }
        if (gui.Input.IsMouseButtonPressed(MouseButton.Left)
            && !state.Anchor.Contains(gui.Input.MousePosition) && !state.PopupRect.Contains(gui.Input.MousePosition))
            state.RequestedOpen = false;
    }
}

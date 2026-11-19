namespace Guinevere;

public static partial class ControlsExtensions
{
    const string BodyId = "table/body";

    /// <summary>
    /// What a pointer does to a row: a press selects it (Ctrl toggles, Shift extends from the anchor) and focuses
    /// the body, a second press activates it, dragging across rows selects the range, and a right-click selects it
    /// and opens the row menu. It runs in the render pass, so a change shows in the next frame's tree.
    /// </summary>
    static void RowInteraction<TRow>(in TableBuild<TRow> b, int row, string key, TRow item)
    {
        var gui = b.Gui;
        var mode = b.Options.Selection;
        var typed = b.Typed;
        if (gui.Pass != Pass.Pass2Render
            || (mode == TableSelectionMode.None && typed?.RowMenu is null && typed?.RowActivated is null))
        {
            return;
        }

        var state = b.State;
        var selection = state.Selection;
        var interactable = gui.GetInteractable();
        if (interactable.OnClick(out var clicks))
        {
            gui.RequestFocus(BodyId, FocusReason.Mouse);
            var ctrl = IsModifierDown(gui, KeyboardKey.LeftControl, KeyboardKey.RightControl);
            var shift = IsModifierDown(gui, KeyboardKey.LeftShift, KeyboardKey.RightShift);
            if (mode != TableSelectionMode.None)
            {
                selection.Click(b.Source, mode, row, key, ctrl, shift);
                state.DragSelecting = mode == TableSelectionMode.Multiple && !ctrl && !shift;
            }
            else
            {
                selection.SetCurrent(row, key);
            }

            if (clicks >= 2 && typed?.RowActivated is { } activated)
            {
                activated(new TableRowContext<TRow>(row, item, new RowIdentity(key), state));
            }
        }
        else if (state.DragSelecting && gui.Input.IsMouseButtonDown(MouseButton.Left) && selection.CurrentIndex != row
                 && interactable.OnHover())
        {
            selection.SelectRange(b.Source, selection.AnchorIndex, row, false);
            selection.SetCurrent(row, key);
        }

        if (typed?.RowMenu is not null && interactable.OnClick(MouseButton.Right))
        {
            if (mode != TableSelectionMode.None && !selection.Contains(key))
            {
                selection.Click(b.Source, mode, row, key, false, false);
            }

            gui.RequestFocus(BodyId, FocusReason.Mouse);
            state.MenuRow = row;
            state.MenuKey = key;
            state.RowMenuOpen = true;
        }
    }

    /// <summary>
    /// The row menu of the last right-click, while it is open. It is built only then, so a table with a menu
    /// pays nothing for it per frame; it closes when the row it belongs to has moved or gone.
    /// </summary>
    static void RowContextMenu<TRow>(in TableBuild<TRow> b)
    {
        var state = b.State;
        if (!state.RowMenuOpen || b.Typed?.RowMenu is not { } build)
        {
            return;
        }

        var row = state.MenuRow;
        if (row < 0 || row >= state.FrameRowCount || b.Source.GetRowIdentity(row).Key != state.MenuKey)
        {
            state.RowMenuOpen = false;
            return;
        }

        var context = new TableRowContext<TRow>(row, b.Source[row], new RowIdentity(state.MenuKey), state);
        b.Gui.ContextMenu(ref state.RowMenuOpen, menu => build(menu, context), id: "table/row-menu");
    }

    /// <summary>
    /// What the keys do while the body has focus: Up, Down, Page Up, Page Down, Home and End move the keyboard
    /// row (Shift extends the selection, Ctrl moves without selecting), Space toggles it, Enter activates it,
    /// Ctrl+A selects every row, and Right and Left expand and collapse its details.
    /// </summary>
    static void BodyKeys<TRow>(in TableBuild<TRow> b)
    {
        var gui = b.Gui;
        var mode = b.Options.Selection;
        var state = b.State;
        if (mode == TableSelectionMode.None || state.FrameRowCount == 0 || !gui.HasFocus(BodyId))
        {
            return;
        }

        var input = gui.Input;
        var ctrl = IsModifierDown(gui, KeyboardKey.LeftControl, KeyboardKey.RightControl);
        var shift = IsModifierDown(gui, KeyboardKey.LeftShift, KeyboardKey.RightShift);
        var selection = state.Selection;
        var last = state.FrameRowCount - 1;
        var current = selection.CurrentIndex;
        var page = Math.Max(1, (int)(state.FrameViewportHeight / b.Theme.RowHeight) - 1);
        int? target = null;
        if (input.IsKeyPressed(KeyboardKey.Down))
        {
            target = current < 0 ? 0 : current + 1;
        }
        else if (input.IsKeyPressed(KeyboardKey.Up))
        {
            target = current < 0 ? 0 : current - 1;
        }
        else if (input.IsKeyPressed(KeyboardKey.PageDown))
        {
            target = current < 0 ? 0 : current + page;
        }
        else if (input.IsKeyPressed(KeyboardKey.PageUp))
        {
            target = current < 0 ? 0 : current - page;
        }
        else if (input.IsKeyPressed(KeyboardKey.Home))
        {
            target = 0;
        }
        else if (input.IsKeyPressed(KeyboardKey.End))
        {
            target = last;
        }

        if (target is { } moved)
        {
            var row = Math.Clamp(moved, 0, last);
            selection.MoveTo(b.Source, mode, row, ctrl, shift);
            EnsureRowVisible(gui, state, b.Theme, row);
        }
        else if (ctrl && mode == TableSelectionMode.Multiple && input.IsKeyPressed(KeyboardKey.A))
        {
            selection.SelectAll(b.Source);
        }
        else if (input.IsKeyPressed(KeyboardKey.Space))
        {
            selection.ToggleCurrent(mode);
        }
        else if (selection.CurrentKey is { } key && current >= 0)
        {
            CurrentRowActions(b, current, key);
        }
    }

    /// <summary>Enter activates the keyboard row, Right expands it and Left collapses it.</summary>
    static void CurrentRowActions<TRow>(in TableBuild<TRow> b, int row, string key)
    {
        var input = b.Gui.Input;
        var enter = input.IsKeyPressed(KeyboardKey.Enter);
        var right = input.IsKeyPressed(KeyboardKey.Right);
        var left = input.IsKeyPressed(KeyboardKey.Left);
        if (!enter && !right && !left)
        {
            return;
        }

        var context = new TableRowContext<TRow>(row, b.Source[row], new RowIdentity(key), b.State);
        if (enter && b.Typed?.RowActivated is { } activated)
        {
            activated(context);
        }
        else if ((right || left) && b.Typed?.RowDetails is { } details && details.HasDetails(context))
        {
            b.State.Expansion.SetExpanded(context.Identity, right, row);
        }
    }

    /// <summary>
    /// Scrolls the body just far enough to bring a row into view. The new offset reaches the row window one
    /// frame later, which the overscan covers for a step and a page; a jump to either end shows one blank frame.
    /// </summary>
    static void EnsureRowVisible(Gui gui, TableState state, TableTheme theme, int row)
    {
        var offsets = state.Geometry.Offsets;
        var top = offsets?.TopOf(row) ?? row * theme.RowHeight;
        var bottom = top + theme.RowHeight;
        var scroll = BodyScroll(gui, state).Y;
        if (top < scroll)
        {
            gui.ScrollBy(BodyId, new Vector2(0f, top - scroll));
        }
        else if (bottom > scroll + state.FrameViewportHeight)
        {
            gui.ScrollBy(BodyId, new Vector2(0f, bottom - scroll - state.FrameViewportHeight));
        }
    }
}

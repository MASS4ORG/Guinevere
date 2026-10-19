namespace Guinevere;

public partial class Gui
{
    TextEditState ProcessTextSelection(LayoutNode node, string text, TextLayout layout, Vector2 origin)
    {
        var state = TextEditor.State(this, $"{node.Id}/selection", text);
        Focus.RegisterFocusableControl(node.Id, node.Parent?.Id, true, true, node.Center);
        Focus.SetClaimsArrowKeys(node.Id, true);
        var interaction = GetInteractable(node);
        var held = interaction.OnHold();
        if (held)
        {
            var at = layout.OffsetAt(LocalPointer(node) - origin);
            if (interaction.OnClick(out var clicks))
            {
                RequestFocus(node.Id, FocusReason.Mouse);
                StartTextSelection(state, text, at, clicks);
            }
            else if (state.IsSelecting)
            {
                state.MoveTo(at, extend: true);
            }
        }
        else
        {
            state.IsSelecting = false;
        }

        state.IsFocused = HasFocus(node.Id);
        if (state.IsFocused)
        {
            HandleTextSelectionKeys(state);
        }

        return state;
    }

    void StartTextSelection(TextEditState state, string text, int at, int clicks)
    {
        state.MoveTo(at, extend: Input.IsKeyDown(KeyboardKey.LeftShift)
            || Input.IsKeyDown(KeyboardKey.RightShift));
        if (clicks >= 3)
        {
            state.SelectAll();
        }
        else if (clicks == 2)
        {
            var (start, end) = TextEditor.WordAt(text, at);
            state.MoveTo(start, extend: false);
            state.MoveTo(end, extend: true);
        }
        state.IsSelecting = clicks == 1;
    }

    void HandleTextSelectionKeys(TextEditState state)
    {
        if (HandleTextSelectionClipboard(state))
        {
            return;
        }

        var extend = Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift);
        if (Input.IsKeyPressed(KeyboardKey.Home))
        {
            state.MoveTo(0, extend);
        }
        else if (Input.IsKeyPressed(KeyboardKey.End))
        {
            state.MoveTo(state.Text.Length, extend);
        }
        else
        {
            HandleTextSelectionArrow(state, extend);
        }
    }

    bool HandleTextSelectionClipboard(TextEditState state)
    {
        var control = Input.IsKeyDown(KeyboardKey.LeftControl) || Input.IsKeyDown(KeyboardKey.RightControl);
        if (!control)
        {
            return false;
        }

        if (Input.IsKeyPressed(KeyboardKey.A))
        {
            state.SelectAll();
        }
        else if (Input.IsKeyPressed(KeyboardKey.C))
        {
            Platform.Require<IClipboard>().SetClipboardText(state.HasSelection ? state.SelectedText : state.Text);
        }
        else
        {
            return false;
        }

        return true;
    }

    void HandleTextSelectionArrow(TextEditState state, bool extend)
    {
        if (Input.IsKeyPressed(KeyboardKey.Left))
        {
            state.MoveTo(!extend && state.HasSelection ? state.SelectionStart : state.CursorPosition - 1, extend);
        }
        else if (Input.IsKeyPressed(KeyboardKey.Right))
        {
            state.MoveTo(!extend && state.HasSelection ? state.SelectionEnd : state.CursorPosition + 1, extend);
        }
    }

    static void DrawTextSelection(LayoutNode node, TextEditState state, TextLayout layout, Vector2 origin)
    {
        if (!state.IsFocused || !state.HasSelection)
        {
            return;
        }

        foreach (var rect in layout.RangeBounds(state.SelectionStart, state.SelectionEnd))
        {
            AddTextRect(node, Color.FromArgb(110, 100, 149, 237), origin.X + rect.X, origin.Y + rect.Y, rect.W,
                rect.H);
        }
    }
}

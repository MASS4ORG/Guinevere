namespace Guinevere;

public partial class Gui
{
    TextEditState ProcessTextSelection(LayoutNode node, string text, IReadOnlyList<WrappedLine> lines,
        Font mainFont, Font iconFont, float lineHeight, bool centered)
    {
        var state = TextEditor.State(this, $"{node.Id}/selection", text);
        Focus.RegisterFocusableControl(node.Id, node.Parent?.Id, true, true, node.Center);
        Focus.SetClaimsArrowKeys(node.Id, true);
        var interaction = GetInteractable(node);
        var held = interaction.OnHold();
        if (held)
        {
            var at = TextSelectionOffset(node.InnerRect, lines, mainFont, iconFont, lineHeight, centered);
            if (interaction.OnClick(out var clicks))
            {
                RequestFocus(node.Id, FocusReason.Mouse);
                StartTextSelection(state, text, at, clicks);
            }
            else if (state.IsSelecting) state.MoveTo(at, extend: true);
        }
        else state.IsSelecting = false;

        state.IsFocused = HasFocus(node.Id);
        if (state.IsFocused) HandleTextSelectionKeys(state);
        return state;
    }

    void StartTextSelection(TextEditState state, string text, int at, int clicks)
    {
        state.MoveTo(at, extend: Input.IsKeyDown(KeyboardKey.LeftShift)
            || Input.IsKeyDown(KeyboardKey.RightShift));
        if (clicks >= 3) state.SelectAll();
        else if (clicks == 2)
        {
            var (start, end) = TextEditor.WordAt(text, at);
            state.MoveTo(start, extend: false);
            state.MoveTo(end, extend: true);
        }
        state.IsSelecting = clicks == 1;
    }

    int TextSelectionOffset(Rect inner, IReadOnlyList<WrappedLine> lines, Font mainFont, Font iconFont,
        float lineHeight, bool centered)
    {
        if (lines.Count == 0) return 0;
        var row = Math.Clamp((int)((Input.MousePosition.Y - inner.Y) / lineHeight), 0, lines.Count - 1);
        var line = lines[row];
        float Measure(string value) => MeasureLineWidth(value, mainFont, iconFont);
        var origin = inner.X + (centered ? Math.Max(0, (inner.W - Measure(line.Text)) * 0.5f) : 0);
        return line.Start + WrappedTextLayout.PositionInLine(Measure, line.Text, Input.MousePosition.X - origin);
    }

    void HandleTextSelectionKeys(TextEditState state)
    {
        if (HandleTextSelectionClipboard(state)) return;
        var extend = Input.IsKeyDown(KeyboardKey.LeftShift) || Input.IsKeyDown(KeyboardKey.RightShift);
        if (Input.IsKeyPressed(KeyboardKey.Home)) state.MoveTo(0, extend);
        else if (Input.IsKeyPressed(KeyboardKey.End)) state.MoveTo(state.Text.Length, extend);
        else HandleTextSelectionArrow(state, extend);
    }

    bool HandleTextSelectionClipboard(TextEditState state)
    {
        var control = Input.IsKeyDown(KeyboardKey.LeftControl) || Input.IsKeyDown(KeyboardKey.RightControl);
        if (!control) return false;
        if (Input.IsKeyPressed(KeyboardKey.A)) state.SelectAll();
        else if (Input.IsKeyPressed(KeyboardKey.C))
            Platform.Require<IClipboard>().SetClipboardText(state.HasSelection ? state.SelectedText : state.Text);
        else return false;
        return true;
    }

    void HandleTextSelectionArrow(TextEditState state, bool extend)
    {
        if (Input.IsKeyPressed(KeyboardKey.Left))
            state.MoveTo(!extend && state.HasSelection ? state.SelectionStart : state.CursorPosition - 1, extend);
        else if (Input.IsKeyPressed(KeyboardKey.Right))
            state.MoveTo(!extend && state.HasSelection ? state.SelectionEnd : state.CursorPosition + 1, extend);
    }

    void DrawTextSelection(LayoutNode node, TextEditState state, WrappedLine line, Font mainFont, Font iconFont,
        Rect row)
    {
        if (!state.IsFocused || !state.HasSelection) return;
        float Measure(string value) => MeasureLineWidth(value, mainFont, iconFont);
        if (WrappedTextLayout.SelectionOn(Measure, line, state.SelectionStart, state.SelectionEnd) is not { } run)
            return;
        var shape = Shape.Rectangle(run.Width, row.H).SolidColor(ControlStyle.TextSelection);
        node.DrawList.Add(new ShapePos(shape.Path, shape.Paint, new Vector2(row.X + run.X, row.Y)));
    }
}

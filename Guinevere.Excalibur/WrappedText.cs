namespace Guinevere;

public static partial class ControlsExtensions
{
    /// <summary>
    /// Draws <paramref name="text"/> into the current node — the content of a <c>gui.ScrollY()</c>
    /// container — wrapped to the container's width, as a read-only label that supports drag-to-select,
    /// Ctrl+A select-all and Ctrl+C copy. The caret is not drawn and no text-field focus is taken, so a
    /// surrounding panel's shortcuts and text fields keep their own meaning; the returned state lets the
    /// caller read the selection.
    /// </summary>
    /// <param name="gui">The GUI instance.</param>
    /// <param name="text">The message to lay out.</param>
    /// <param name="size">The font size the label is drawn at.</param>
    /// <param name="measureFont">The font measuring glyphs for wrap and hit-testing — Skia's, since the
    /// draw font keeps its own internal to Guinevere. Must be the same typeface and size as
    /// <paramref name="drawFont"/> for the selection to line up with the glyphs.</param>
    /// <param name="color">The text color.</param>
    /// <param name="selectionColor">The selection highlight color.</param>
    /// <param name="drawFont">The font to draw with, or null for the GUI's default.</param>
    /// <param name="copyable">Whether Ctrl+C copies the selection, or the whole text when nothing is selected.</param>
    /// <returns>The message's selection state.</returns>
    public static TextEditState WrappedLabel(this Gui gui, string text, float size, SKFont measureFont,
        Color color, Color? selectionColor = null, Font? drawFont = null, bool copyable = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measureFont);

        var nodeId = gui.CurrentNode.Id;
        var inner = gui.CurrentNode.InnerRect;
        var layout = gui.CurrentNodeScope.Get<LayoutNodeScopeTextLayout>().Value;
        var lineHeight = size * layout.LineHeight;

        // The width the frame lays out with. Captured during the render pass, when the inner rect is
        // final; the build pass then reuses the last captured value, so both passes wrap to the same
        // width and produce the same node tree.
        ref var width = ref gui.GetValue(200f, $"{nodeId}/wrappedLabelWidth");
        if (gui.Pass == Pass.Pass2Render) width = Math.Max(200f, inner.W);

        var primary = new Font(new SKFont(drawFont?.SkFont.Typeface ?? measureFont.Typeface, size));
        var emoji = new Font(new SKFont(
            gui.CurrentNodeScope.Get<LayoutNodeScopeIconFont>().Value.SkFont.Typeface, size));
        float Measure(string value)
        {
            var total = 0f;
            foreach (var run in gui.CreateTextRuns(value, primary, emoji))
                total += run.Font.SkFont.MeasureText(run.Text);
            return total;
        }

        var lines = WrappedTextLayout.Wrap(text, width, Measure, layout);
        var state = TextEditor.State(gui, $"{nodeId}/wrappedLabelText", text);
        var offsetY = gui.GetScrollState(nodeId)?.ScrollOffset.Y ?? 0f;

        if (gui.Pass == Pass.Pass2Render)
        {
            Select(gui, state, lines, Measure, inner, offsetY, lineHeight);
            if (copyable) HandleLabelShortcuts(gui, state, text);
        }

        var highlight = selectionColor ?? gui.ControlStyle.TextSelection;
        for (var index = 0; index < lines.Count; index++)
        {
            var top = inner.Y - offsetY + (index * lineHeight);
            using (gui.Node(-1, lineHeight, $"{nodeId}/wrappedLabelLine/{index}").ExpandWidth().Enter())
                DrawWrappedLine(gui, state, lines[index], Measure, new Rect(inner.X, top, inner.W, lineHeight),
                    size, color, highlight, drawFont);
        }

        return state;
    }

    /// <summary>Ctrl+A selects everything; Ctrl+C copies the selection, or the whole text with none.</summary>
    static void HandleLabelShortcuts(Gui gui, TextEditState state, string text)
    {
        if (gui.Focus.IsTextInputFocused || !IsControlDown(gui)) return;

        if (gui.Input.IsKeyPressed(KeyboardKey.A)) state.SelectAll();
        else if (gui.Input.IsKeyPressed(KeyboardKey.C))
            gui.Platform.Require<IClipboard>().SetClipboardText(state.HasSelection ? state.SelectedText : text);
    }

    /// <summary>Draws one wrapped line into the current node, under its share of the selection highlight.</summary>
    static void DrawWrappedLine(Gui gui, TextEditState state, WrappedLine line, Func<string, float> measure, Rect row,
        float size, Color color, Color highlight, Font? drawFont)
    {
        if (gui.Pass == Pass.Pass2Render && state.HasSelection &&
            WrappedTextLayout.SelectionOn(measure, line, state.SelectionStart, state.SelectionEnd) is { } run)
            gui.DrawRectFilled(new Rect(row.X + run.X, row.Y, run.Width, row.H), highlight);

        if (line.Text.Length > 0) gui.DrawText(line.Text, size, color, drawFont, centerInRect: false, clip: true);
    }

    /// <summary>
    /// Pointer select: a press inside the pane anchors the selection, a drag extends it, a release
    /// ends it. No text-field focus is registered, so surrounding shortcuts keep their meaning.
    /// </summary>
    static void Select(Gui gui, TextEditState state, IReadOnlyList<WrappedLine> lines,
        Func<string, float> measure, Rect inner, float offsetY, float lineHeight)
    {
        var mouse = gui.Input.MousePosition;
        var inside = inner.Contains(mouse);
        var left = MouseButton.Left;

        if (gui.Input.IsMouseButtonPressed(left) && inside)
        {
            state.MoveTo(OffsetAt(lines, measure, mouse, inner, offsetY, lineHeight), extend: false);
            state.IsSelecting = true;
        }
        else if (state.IsSelecting && gui.Input.IsMouseButtonDown(left))
        {
            state.MoveTo(OffsetAt(lines, measure, mouse, inner, offsetY, lineHeight), extend: true);
        }
        else if (state.IsSelecting && !gui.Input.IsMouseButtonDown(left))
        {
            state.IsSelecting = false;
        }
    }

    /// <summary>The message offset nearest a screen point, from the wrapped lines' geometry.</summary>
    static int OffsetAt(IReadOnlyList<WrappedLine> lines, Func<string, float> measure, Vector2 point,
        Rect inner, float offsetY, float lineHeight)
    {
        if (lines.Count == 0) return 0;

        var row = (int)Math.Floor((point.Y - inner.Y + offsetY) / lineHeight);
        row = Math.Clamp(row, 0, lines.Count - 1);

        var line = lines[row];
        var column = WrappedTextLayout.PositionInLine(measure, line.Text, point.X - inner.X);
        return Math.Clamp(line.Start + column, line.Start, line.Start + line.Text.Length);
    }

    static bool IsControlDown(Gui gui) =>
        gui.Input.IsKeyDown(KeyboardKey.LeftControl) || gui.Input.IsKeyDown(KeyboardKey.RightControl);
}

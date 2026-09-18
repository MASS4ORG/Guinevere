using System.Numerics;
using SkiaSharp;

namespace Guinevere;

/// <summary>One visual line of a wrapped message, with where it starts in the original text.</summary>
/// <param name="Text">The line's characters, as drawn.</param>
/// <param name="Start">Offset of the line's first character in the message.</param>
public readonly record struct WrappedLine(string Text, int Start);

/// <summary>A horizontal selection run inside one wrapped line, in pixels relative to the line's left edge.</summary>
/// <param name="X">Where the run starts, relative to the line's left edge.</param>
/// <param name="Width">The run's width.</param>
/// <param name="Start">The run's character offset.</param>
/// <param name="End">The first character after the run.</param>
public readonly record struct LineSelection(float X, float Width, int Start, int End);

/// <summary>
/// Lays a message out into visual lines: greedy word wrap to a width, and the coordinate/offset
/// mappings that let a pane implement drag-to-select over the wrapped result. Kept free of GUI types
/// so the wrapping and hit-testing rules can be tested on their own; the pane supplies the font and
/// the rectangles.
/// </summary>
public static class WrappedTextLayout
{
    /// <summary>Height one wrapped line occupies for a given font size, matching the text renderer.</summary>
    /// <param name="fontSize">The font size the text is drawn at.</param>
    /// <returns>The line height in pixels.</returns>
    public static float LineHeight(float fontSize) => fontSize * 1.2f;

    /// <summary>
    /// Wraps text to a width by greedy word join, splitting paragraphs on newlines. Each returned line
    /// carries its offset in the original message, so a click on the line maps back to a character in
    /// the un-wrapped text.
    /// </summary>
    /// <param name="text">The message to wrap.</param>
    /// <param name="font">The font measuring the glyphs.</param>
    /// <param name="maxWidth">The width lines may occupy, in pixels.</param>
    /// <returns>The visual lines.</returns>
    public static IReadOnlyList<WrappedLine> Wrap(string text, SKFont font, float maxWidth)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);

        if (maxWidth <= 0f)
            return text.Split('\n').Select((line) => new WrappedLine(line, 0)).ToList();

        var lines = new List<WrappedLine>();
        var paragraphStart = 0;

        foreach (var paragraph in text.Split('\n'))
        {
            AppendParagraph(lines, paragraph, paragraphStart, font, maxWidth);
            paragraphStart += paragraph.Length + 1;
        }

        return lines;
    }

    static void AppendParagraph(List<WrappedLine> lines, string paragraph, int paragraphStart,
        SKFont font, float maxWidth)
    {
        if (paragraph.Length == 0)
        {
            lines.Add(new WrappedLine("", paragraphStart));
            return;
        }

        var current = "";
        var currentStart = paragraphStart;
        var searchFrom = 0;

        foreach (var word in paragraph.Split(' '))
        {
            var relative = paragraph.IndexOf(word, searchFrom, StringComparison.Ordinal);
            var wordStart = paragraphStart + relative;
            searchFrom = relative + word.Length + 1;

            if (current.Length == 0)
            {
                if (font.MeasureText(word) <= maxWidth)
                {
                    current = word;
                    currentStart = wordStart;
                }
                else
                {
                    // A single word wider than the pane still gets a line of its own.
                    lines.Add(new WrappedLine(word, wordStart));
                }

                continue;
            }

            var candidate = current + " " + word;
            if (font.MeasureText(candidate) <= maxWidth)
            {
                current = candidate;
            }
            else
            {
                lines.Add(new WrappedLine(current, currentStart));
                current = font.MeasureText(word) <= maxWidth ? word : "";
                if (current.Length > 0) currentStart = wordStart;
                else lines.Add(new WrappedLine(word, wordStart));
            }
        }

        if (current.Length > 0) lines.Add(new WrappedLine(current, currentStart));
    }

    /// <summary>The character offset nearest a point on one wrapped line.</summary>
    /// <param name="font">Font the line is measured with.</param>
    /// <param name="line">The line's text.</param>
    /// <param name="clickX">The x to locate, relative to the line's left edge.</param>
    /// <returns>The column within the line.</returns>
    public static int PositionInLine(SKFont font, string line, float clickX)
    {
        ArgumentNullException.ThrowIfNull(font);

        return string.IsNullOrEmpty(line) ? 0 : NearestColumn(font, line, clickX);
    }

    static int NearestColumn(SKFont font, string line, float clickX)
    {
        var best = 0;
        var bestDistance = float.MaxValue;

        for (var index = 0; index <= line.Length; index++)
        {
            var distance = Math.Abs(clickX - font.MeasureText(line[..index]));
            if (distance >= bestDistance) continue;

            best = index;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// The run of <paramref name="selectionStart"/>..<paramref name="selectionEnd"/> that falls inside
    /// the given line, or null when that line holds no selected characters.
    /// </summary>
    /// <param name="font">Font measuring the line's prefix widths.</param>
    /// <param name="line">The wrapped line, with its offset in the message.</param>
    /// <param name="selectionStart">The selection's lower bound.</param>
    /// <param name="selectionEnd">The selection's upper bound.</param>
    /// <returns>The highlight run, or null when the line is entirely outside the selection.</returns>
    public static LineSelection? SelectionOn(SKFont font, WrappedLine line, int selectionStart, int selectionEnd)
    {
        ArgumentNullException.ThrowIfNull(font);

        var from = Math.Max(line.Start, selectionStart);
        var to = Math.Min(line.Start + line.Text.Length, selectionEnd);
        if (from >= to) return null;

        var x = font.MeasureText(line.Text[..(from - line.Start)]);
        var width = font.MeasureText(line.Text[..(to - line.Start)]) - x;
        return new LineSelection(x, Math.Max(1f, width), from, to);
    }
}

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
        var lineHeight = WrappedTextLayout.LineHeight(size);

        // The width the frame lays out with. Captured during the render pass, when the inner rect is
        // final; the build pass then reuses the last captured value, so both passes wrap to the same
        // width and produce the same node tree.
        ref var width = ref gui.GetValue(200f, $"{nodeId}/wrappedLabelWidth");
        if (gui.Pass == Pass.Pass2Render) width = Math.Max(200f, inner.W);

        var lines = WrappedTextLayout.Wrap(text, measureFont, width);
        var state = TextEditor.State(gui, $"{nodeId}/wrappedLabelText", text);
        var offsetY = gui.GetScrollState(nodeId)?.ScrollOffset.Y ?? 0f;

        if (gui.Pass == Pass.Pass2Render)
        {
            Select(gui, state, lines, measureFont, inner, offsetY, lineHeight);

            if (copyable && !gui.Focus.IsTextInputFocused && IsControlDown(gui))
            {
                if (gui.Input.IsKeyPressed(KeyboardKey.A)) state.SelectAll();
                else if (gui.Input.IsKeyPressed(KeyboardKey.C))
                    gui.Input.SetClipboardText(state.HasSelection ? state.SelectedText : text);
            }
        }

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var top = inner.Y - offsetY + (index * lineHeight);

            using (gui.Node(-1, lineHeight, $"{nodeId}/wrappedLabelLine/{index}").ExpandWidth().Enter())
            {
                if (gui.Pass == Pass.Pass2Render && state.HasSelection)
                {
                    if (WrappedTextLayout.SelectionOn(measureFont, line, state.SelectionStart, state.SelectionEnd)
                        is { } run)
                        gui.DrawRectFilled(new Rect(inner.X + run.X, top, run.Width, lineHeight),
                            selectionColor ?? Color.FromArgb(80, 44, 130, 255));
                }

                if (line.Text.Length > 0)
                    gui.DrawText(line.Text, size, color, drawFont, centerInRect: false, clip: true);
            }
        }

        return state;
    }

    /// <summary>
    /// Pointer select: a press inside the pane anchors the selection, a drag extends it, a release
    /// ends it. No text-field focus is registered, so surrounding shortcuts keep their meaning.
    /// </summary>
    static void Select(Gui gui, TextEditState state, IReadOnlyList<WrappedLine> lines,
        SKFont font, Rect inner, float offsetY, float lineHeight)
    {
        var mouse = gui.Input.MousePosition;
        var inside = inner.Contains(mouse);
        var left = MouseButton.Left;

        if (gui.Input.IsMouseButtonPressed(left) && inside)
        {
            state.MoveTo(OffsetAt(lines, font, mouse, inner, offsetY, lineHeight), extend: false);
            state.IsSelecting = true;
        }
        else if (state.IsSelecting && gui.Input.IsMouseButtonDown(left))
        {
            state.MoveTo(OffsetAt(lines, font, mouse, inner, offsetY, lineHeight), extend: true);
        }
        else if (state.IsSelecting && !gui.Input.IsMouseButtonDown(left))
        {
            state.IsSelecting = false;
        }
    }

    /// <summary>The message offset nearest a screen point, from the wrapped lines' geometry.</summary>
    static int OffsetAt(IReadOnlyList<WrappedLine> lines, SKFont font, Vector2 point,
        Rect inner, float offsetY, float lineHeight)
    {
        if (lines.Count == 0) return 0;

        var row = (int)Math.Floor((point.Y - inner.Y + offsetY) / lineHeight);
        row = Math.Clamp(row, 0, lines.Count - 1);

        var line = lines[row];
        var column = WrappedTextLayout.PositionInLine(font, line.Text, point.X - inner.X);
        return Math.Clamp(line.Start + column, line.Start, line.Start + line.Text.Length);
    }

    static bool IsControlDown(Gui gui) =>
        gui.Input.IsKeyDown(KeyboardKey.LeftControl) || gui.Input.IsKeyDown(KeyboardKey.RightControl);
}

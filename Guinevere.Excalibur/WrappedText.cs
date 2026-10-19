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
    /// <param name="size">The logical font size, or zero for the scope's.</param>
    /// <param name="font">The font to draw with, or null for the scope's.</param>
    /// <param name="copyable">Whether Ctrl+C copies the selection, or the whole text when nothing is selected.</param>
    /// <param name="classes">Stylesheet classes for the label.</param>
    /// <param name="id">Stable stylesheet identity.</param>
    /// <returns>The message's selection state.</returns>
    public static TextEditState WrappedLabel(this Gui gui, string text, float size = 0, Font? font = null,
        bool copyable = true, IReadOnlyList<string>? classes = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ExcaliburStyles.Ensure(gui);

        var viewport = gui.CurrentNode.InnerRect;
        using var scope = gui.StyledNode("wrapped-label", classes, id).ExpandWidth().Enter();
        var labelScope = gui.CurrentNode.Scope;
        var color = labelScope.Get<LayoutNodeScopeTextColor>().Value;
        var options = labelScope.Get<LayoutNodeScopeTextLayout>().Value;
        var (node, layout) = gui.TextBlock(RichTextContent.Plain(text), font, size, TextLayoutOptions.FillWidth,
            options, TextAlignment.Start);
        var state = TextEditor.State(gui, $"{gui.CurrentNode.Id}/wrappedLabelText", text);
        if (gui.Pass != Pass.Pass2Render)
        {
            return state;
        }

        var origin = node.InnerRect.Position;
        Select(gui, state, layout, origin, viewport);
        if (copyable)
        {
            HandleLabelShortcuts(gui, state, text);
        }

        if (state.HasSelection)
        {
            var highlight = gui.ResolvePart("selection");
            foreach (var rect in layout.RangeBounds(state.SelectionStart, state.SelectionEnd))
            {
                gui.DrawStyledBox(highlight, new Rect(origin.X + rect.X, origin.Y + rect.Y, rect.W, rect.H));
            }
        }

        gui.DrawTextLayout(layout, origin, color, null, false, node);
        return state;
    }

    /// <summary>Ctrl+A selects everything; Ctrl+C copies the selection, or the whole text with none.</summary>
    static void HandleLabelShortcuts(Gui gui, TextEditState state, string text)
    {
        if (gui.Focus.IsTextInputFocused || !IsControlDown(gui))
        {
            return;
        }

        if (gui.Input.IsKeyPressed(KeyboardKey.A))
        {
            state.SelectAll();
        }
        else if (gui.Input.IsKeyPressed(KeyboardKey.C))
        {
            gui.Platform.Require<IClipboard>().SetClipboardText(state.HasSelection ? state.SelectedText : text);
        }
    }

    /// <summary>
    /// Pointer select: a press inside the pane anchors the selection, a drag extends it, a release
    /// ends it. No text-field focus is registered, so surrounding shortcuts keep their meaning.
    /// </summary>
    static void Select(Gui gui, TextEditState state, TextLayout layout, Vector2 origin, Rect viewport)
    {
        var at = layout.OffsetAt(gui.LocalMousePosition - origin);
        var left = MouseButton.Left;

        if (gui.Input.IsMouseButtonPressed(left) && gui.IsPointerIn(viewport))
        {
            state.MoveTo(at, extend: false);
            state.IsSelecting = true;
        }
        else if (state.IsSelecting && gui.Input.IsMouseButtonDown(left))
        {
            state.MoveTo(at, extend: true);
        }
        else if (state.IsSelecting && !gui.Input.IsMouseButtonDown(left))
        {
            state.IsSelecting = false;
        }
    }

    static bool IsControlDown(Gui gui) =>
        gui.Input.IsKeyDown(KeyboardKey.LeftControl) || gui.Input.IsKeyDown(KeyboardKey.RightControl);
}

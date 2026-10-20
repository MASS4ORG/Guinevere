using System.Runtime.CompilerServices;

namespace Guinevere;

/// <summary>The text editor control.</summary>
public static class TextEditorGui
{
    /// <summary>
    /// Draws a text editor and applies this frame's input to it. Only the visible lines are laid out and drawn, so the
    /// cost of a frame does not grow with the document. Measuring, drawing and hit testing use the shared text layout,
    /// so carets and selections sit exactly on the shaped glyphs.
    /// </summary>
    /// <param name="gui">The GUI.</param>
    /// <param name="state">The editor's state, kept by the caller across frames.</param>
    /// <param name="options">Fonts, gutter and colors, or null for the defaults.</param>
    /// <param name="width">Width in pixels, or zero to fill the parent.</param>
    /// <param name="height">Height in pixels, or zero to fill the parent.</param>
    /// <param name="id">A stable node id, or null to derive one from the call site.</param>
    /// <param name="filePath">Call-site file, for the node id.</param>
    /// <param name="lineNumber">Call-site line, for the node id.</param>
    /// <returns>The editor's node and whether its text changed.</returns>
    public static TextEditorResult TextEditor(this Gui gui, TextEditorState state, TextEditorOptions? options = null,
        float width = 0, float height = 0, string? id = null, [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(gui);
        ArgumentNullException.ThrowIfNull(state);
        options ??= TextEditorOptions.Default;
        var version = state.Document.Version;
        var node = gui.Node(width > 0 ? width : -1, height > 0 ? height : -1, id, filePath, lineNumber);
        if (width == 0)
        {
            node.ExpandWidth();
        }

        if (height == 0)
        {
            node.ExpandHeight();
        }

        using (node.Enter())
        {
            new TextEditorView(gui, state, options, node).Frame();
        }

        return new TextEditorResult(node, state.Document.Version != version);
    }
}

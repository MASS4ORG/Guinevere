namespace Guinevere;

/// <summary>
/// The editing actions of a text editor by name, each applied to every caret. Key bindings, menus and scripts all
/// run them through <see cref="TextEditorState.Execute"/>; <see cref="Register"/> adds or replaces one.
/// </summary>
public static partial class EditorCommands
{
    static readonly Dictionary<string, Action<TextEditorState>> Commands = new(StringComparer.Ordinal);

    static EditorCommands()
    {
        RegisterMovement();
        RegisterEditing();
        RegisterLines();
        RegisterSelection();
        RegisterClipboard();
        RegisterFind();
    }

    /// <summary>The names of every registered command.</summary>
    public static IEnumerable<string> Names => Commands.Keys;

    /// <summary>Adds or replaces a command.</summary>
    public static void Register(string name, Action<TextEditorState> command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(command);
        Commands[name] = command;
    }

    /// <summary>Runs a command; returns false when no command has that name.</summary>
    public static bool Run(string name, TextEditorState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Commands.TryGetValue(name, out var command))
        {
            return false;
        }

        command(state);
        return true;
    }

    /// <summary>A command that edits, which does nothing in a read-only editor.</summary>
    static Action<TextEditorState> Editing(Action<TextEditorState> command) => state =>
    {
        if (!state.Settings.ReadOnly)
        {
            command(state);
            state.RevealCaret = true;
        }
    };

    /// <summary>
    /// Applies one optional edit per selection. <paramref name="edit"/> returns the edit and the selection after
    /// it, both in offsets of the text with only that edit applied; the results are combined into one change.
    /// </summary>
    static void EditEach(TextEditorState state, Func<TextSelection, (TextEdit Edit, TextSelection After)?> edit,
        EditKind kind = EditKind.Other)
    {
        var document = state.Document;
        var edits = new List<TextEdit>();
        var after = new List<TextSelection>();
        var shift = 0;
        var lastEnd = -1;
        foreach (var selection in document.Selections)
        {
            if (edit(selection) is not { } result || result.Edit.Start < lastEnd)
            {
                after.Add(Shifted(selection, shift));
                continue;
            }

            edits.Add(result.Edit);
            after.Add(Shifted(result.After, shift));
            shift += result.Edit.Delta;
            lastEnd = result.Edit.End;
        }

        document.Apply(edits, after, kind, document.PrimaryIndex);
    }

    static TextSelection Shifted(TextSelection selection, int shift) =>
        new(selection.Anchor + shift, selection.Caret + shift, selection.Column);

    /// <summary>Applies edits and moves every selection through them, as indentation changes do.</summary>
    static void ApplyMapped(TextEditorState state, List<TextEdit> edits, EditKind kind = EditKind.Other)
    {
        var document = state.Document;
        edits.Sort((a, b) => a.Start.CompareTo(b.Start));
        var after = document.Selections.Select(selection => new TextSelection(
            TextEdit.Map(selection.Anchor, edits), TextEdit.Map(selection.Caret, edits))).ToList();
        document.Apply(edits, after, kind, document.PrimaryIndex);
    }

    /// <summary>
    /// The lines each selection covers, merged and sorted. A selection ending at the start of a line does not cover
    /// that line, as in every editor.
    /// </summary>
    static List<(int First, int Last)> SelectedLineBlocks(TextEditorState state)
    {
        var buffer = state.Document.Buffer;
        var blocks = new List<(int First, int Last)>();
        foreach (var selection in state.Document.Selections)
        {
            var first = buffer.LineOf(selection.Start);
            var last = buffer.LineOf(selection.End);
            if (last > first && buffer.LineStart(last) == selection.End)
            {
                last--;
            }

            if (blocks.Count > 0 && first <= blocks[^1].Last + 1)
            {
                blocks[^1] = (blocks[^1].First, Math.Max(blocks[^1].Last, last));
            }
            else
            {
                blocks.Add((first, last));
            }
        }

        return blocks;
    }

    /// <summary>Offset after a line's break, or the text's end for the last line.</summary>
    static int LineEndWithBreak(ITextBuffer buffer, int line) =>
        line + 1 < buffer.LineCount ? buffer.LineStart(line + 1) : buffer.Length;
}

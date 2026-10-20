namespace Guinevere;

public static partial class EditorCommands
{
    static void RegisterEditing()
    {
        Register("newline", Editing(NewLine));
        Register("deleteLeft", Editing(state => DeleteLeft(state)));
        Register("deleteRight", Editing(state => Delete(state, (buffer, caret) =>
            (caret, TextNavigation.NextBoundary(buffer, caret)))));
        Register("deleteWordLeft", Editing(state => Delete(state, (buffer, caret) =>
            (TextNavigation.WordLeft(buffer, caret), caret))));
        Register("deleteWordRight", Editing(state => Delete(state, (buffer, caret) =>
            (caret, TextNavigation.WordRight(buffer, caret)))));
        Register("tab", Editing(Tab));
        Register("outdent", Editing(state => Indent(state, outdent: true)));
        Register("indentLines", Editing(state => Indent(state, outdent: false)));
        Register("outdentLines", Editing(state => Indent(state, outdent: true)));
        Register("undo", Editing(state => state.Document.Undo()));
        Register("redo", Editing(state => state.Document.Redo()));
    }

    /// <summary>
    /// Types text at every caret. A lone opening bracket or quote also inserts its closing one, wraps a selection,
    /// and typing a closing character that is already next to the caret steps over it.
    /// </summary>
    internal static void Type(TextEditorState state, string text)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(text);
        if (state.Settings.ReadOnly || text.Length == 0)
        {
            return;
        }

        var buffer = state.Document.Buffer;
        var language = state.Language;
        var pair = text.Length == 1 && state.Settings.AutoClose ? ClosingOf(language, text[0]) : null;
        EditEach(state, selection =>
        {
            if (text.Length == 1 && selection.IsEmpty && selection.Caret < buffer.Length
                && buffer[selection.Caret] == text[0] && IsClosing(language, text[0]))
            {
                return (new TextEdit(selection.Caret, 0, ""), TextSelection.At(selection.Caret + 1));
            }

            if (pair is { } close && (!selection.IsEmpty || ShouldClose(buffer, selection.Caret, text[0], close)))
            {
                var inner = buffer.GetText(selection.Start, selection.Length);
                return (new TextEdit(selection.Start, selection.Length, text + inner + close),
                    new TextSelection(selection.Start + 1, selection.Start + 1 + inner.Length));
            }

            return (new TextEdit(selection.Start, selection.Length, text),
                TextSelection.At(selection.Start + text.Length));
        }, text.Contains('\n') ? EditKind.Other : EditKind.Typing);
        state.RevealCaret = true;
        if (state.Settings.SuggestWhileTyping && text.Length == 1 && TextNavigation.IsWordChar(text[0])
            && state.Completion is null)
        {
            state.TriggerCompletion();
        }
    }

    static char? ClosingOf(LanguageServices language, char open)
    {
        foreach (var (o, c) in language.Brackets)
        {
            if (o == open)
            {
                return c;
            }
        }

        return language.Quotes.Contains(open) ? open : null;
    }

    static bool IsClosing(LanguageServices language, char value) =>
        language.Quotes.Contains(value) || language.Brackets.Any(pair => pair.Close == value);

    /// <summary>Whether to close a bracket or quote typed at a caret: before blank or closing text only.</summary>
    static bool ShouldClose(ITextBuffer buffer, int caret, char open, char close)
    {
        var next = caret < buffer.Length ? buffer[caret] : '\n';
        if (!char.IsWhiteSpace(next) && next is not (')' or ']' or '}' or ',' or ';'))
        {
            return false;
        }

        if (open != close)
        {
            return true;
        }

        var previous = caret > 0 ? buffer[caret - 1] : ' ';
        return !TextNavigation.IsWordChar(previous) && previous != open;
    }

    /// <summary>Breaks the line, keeping its indentation and opening an indented line between brackets.</summary>
    static void NewLine(TextEditorState state)
    {
        var buffer = state.Document.Buffer;
        EditEach(state, selection =>
        {
            var line = buffer.LineOf(selection.Start);
            var indent = state.Settings.AutoIndent ? TextNavigation.Indentation(buffer.Line(line)) : "";
            var before = selection.Start > buffer.LineStart(line) ? buffer[selection.Start - 1] : '\0';
            var after = selection.End < buffer.Length ? buffer[selection.End] : '\0';
            var opens = state.Settings.AutoIndent && state.Language.Brackets.Any(pair => pair.Open == before);
            var text = "\n" + indent + (opens ? state.Settings.IndentUnit : "");
            var caret = selection.Start + text.Length;
            if (opens && state.Language.Brackets.Any(pair => pair.Open == before && pair.Close == after))
            {
                text += "\n" + indent;
            }

            return (new TextEdit(selection.Start, selection.Length, text), TextSelection.At(caret));
        });
    }

    /// <summary>
    /// Deletes before each caret: a selection, an empty bracket pair around the caret, spaces back to the previous
    /// tab stop inside the indentation, or else one text element.
    /// </summary>
    static void DeleteLeft(TextEditorState state)
    {
        var buffer = state.Document.Buffer;
        Delete(state, (_, caret) =>
        {
            if (caret == 0)
            {
                return (0, 0);
            }

            var line = buffer.LineOf(caret);
            var start = buffer.LineStart(line);
            var before = buffer.GetText(start, caret - start);
            if (state.Settings.InsertSpaces && before.Length > 0 && before.All(c => c == ' '))
            {
                var tab = state.Settings.TabSize;
                return (start + (before.Length - 1) / tab * tab, caret);
            }

            var previous = buffer[caret - 1];
            var next = caret < buffer.Length ? buffer[caret] : '\0';
            if (state.Settings.AutoClose && ClosingOf(state.Language, previous) == next && next != '\0')
            {
                return (caret - 1, caret + 1);
            }

            return (TextNavigation.PreviousBoundary(buffer, caret), caret);
        });
    }

    /// <summary>Deletes each selection, or the range <paramref name="range"/> gives around an empty one.</summary>
    static void Delete(TextEditorState state, Func<ITextBuffer, int, (int Start, int End)> range)
    {
        var buffer = state.Document.Buffer;
        EditEach(state, selection =>
        {
            var (start, end) = selection.IsEmpty ? range(buffer, selection.Caret) : (selection.Start, selection.End);
            return end <= start ? null : (new TextEdit(start, end - start, ""), TextSelection.At(start));
        }, EditKind.Deleting);
    }

    /// <summary>Indents the selected lines when a selection spans lines; otherwise inserts up to the next stop.</summary>
    static void Tab(TextEditorState state)
    {
        var buffer = state.Document.Buffer;
        if (state.Document.Selections.Any(selection => buffer.LineOf(selection.Start) != buffer.LineOf(selection.End)))
        {
            Indent(state, outdent: false);
            return;
        }

        var tab = state.Settings.TabSize;
        EditEach(state, selection =>
        {
            var line = buffer.LineOf(selection.Start);
            var column = TextNavigation.VisualColumn(buffer.Line(line), selection.Start - buffer.LineStart(line), tab);
            var text = state.Settings.InsertSpaces ? new string(' ', tab - column % tab) : "\t";
            return (new TextEdit(selection.Start, selection.Length, text), TextSelection.At(selection.Start + text.Length));
        }, EditKind.Typing);
    }

    /// <summary>Adds or removes one indentation level at the start of every selected line.</summary>
    static void Indent(TextEditorState state, bool outdent)
    {
        var buffer = state.Document.Buffer;
        var unit = state.Settings.IndentUnit;
        var edits = new List<TextEdit>();
        foreach (var (first, last) in SelectedLineBlocks(state))
        {
            for (var line = first; line <= last; line++)
            {
                var text = buffer.Line(line);
                var start = buffer.LineStart(line);
                if (!outdent)
                {
                    if (text.Length > 0 || first == last)
                    {
                        edits.Add(new TextEdit(start, 0, unit));
                    }

                    continue;
                }

                var remove = text.StartsWith('\t') ? 1 : text.TakeWhile(c => c == ' ').Take(state.Settings.TabSize).Count();
                if (remove > 0)
                {
                    edits.Add(new TextEdit(start, remove, ""));
                }
            }
        }

        ApplyMapped(state, edits);
    }
}

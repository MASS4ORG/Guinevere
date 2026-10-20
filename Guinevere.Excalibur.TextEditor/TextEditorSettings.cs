namespace Guinevere;

/// <summary>Editing behavior of a text editor, independent of how it looks.</summary>
public sealed record TextEditorSettings
{
    /// <summary>Columns between tab stops.</summary>
    public int TabSize { get; init; } = 4;

    /// <summary>Whether Tab and indentation insert spaces instead of a tab character.</summary>
    public bool InsertSpaces { get; init; } = true;

    /// <summary>Whether typing an opening bracket or quote also inserts its closing one.</summary>
    public bool AutoClose { get; init; } = true;

    /// <summary>Whether a new line keeps the indentation of the line before it.</summary>
    public bool AutoIndent { get; init; } = true;

    /// <summary>Whether typing a word character asks the completion provider for suggestions.</summary>
    public bool SuggestWhileTyping { get; init; } = true;

    /// <summary>Whether the editor is read-only: navigation and copying work, edits do nothing.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>One level of indentation.</summary>
    public string IndentUnit => InsertSpaces ? new string(' ', TabSize) : "\t";
}

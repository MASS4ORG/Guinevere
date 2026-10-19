namespace Guinevere;

/// <summary>How text is broken when a width is supplied.</summary>
public enum TextWrapMode
{
    /// <summary>Only explicit newlines break lines.</summary>
    None,
    /// <summary>Break at spaces; long words may exceed the width.</summary>
    Word,
    /// <summary>Break at Unicode text-element boundaries.</summary>
    Character,
    /// <summary>Break at spaces, then split words wider than the width.</summary>
    WordThenCharacter
}

/// <summary>Letter case text is drawn in, leaving the source string unchanged.</summary>
public enum TextCase
{
    /// <summary>As written.</summary>
    None,
    /// <summary>Every letter uppercase.</summary>
    Upper,
    /// <summary>Every letter lowercase.</summary>
    Lower,
    /// <summary>The first letter of each word uppercase.</summary>
    Capitalize
}

/// <summary>Display layout options shared by measurement and drawing.</summary>
public sealed record TextLayoutOptions
{
    /// <summary>Wrap width that wraps text to the width layout gives its node, reflowing in the same frame.</summary>
    public const float FillWidth = -1f;

    /// <summary>Wrapping behavior when a positive width is supplied.</summary>
    public TextWrapMode WrapMode { get; init; } = TextWrapMode.Word;
    /// <summary>Line box height as a multiple of font size.</summary>
    public float LineHeight { get; init; } = 1.2f;
    /// <summary>Maximum number of visible lines; zero means unlimited.</summary>
    public int MaxLines { get; init; }
    /// <summary>Marker shown when lines are removed by <see cref="MaxLines"/>.</summary>
    public string Ellipsis { get; init; } = "…";
    /// <summary>Extra space after every character, in ems of its font size; spans may override it.</summary>
    public float LetterSpacing { get; init; }
    /// <summary>Extra space added to every space character, in ems of its font size; spans may override it.</summary>
    public float WordSpacing { get; init; }
    /// <summary>Distance between fixed tab stops, in widths of a space of the inherited font.</summary>
    public float TabSize { get; init; } = 4f;
    /// <summary>Explicit tab stops in logical pixels from the block's start edge, used before the fixed ones.</summary>
    public IReadOnlyList<float> TabStops { get; init; } = [];
    /// <summary>Logical pixels the first line of each paragraph is indented.</summary>
    public float FirstLineIndent { get; init; }
    /// <summary>Logical pixels every line after the first of each paragraph is indented.</summary>
    public float HangingIndent { get; init; }
    /// <summary>Line placement, or null to let the drawing call decide.</summary>
    public TextAlignment? Alignment { get; init; }
    /// <summary>Placement of a justified paragraph's last line.</summary>
    public TextAlignment LastLineAlignment { get; init; } = TextAlignment.Start;
    /// <summary>Letter case drawn without changing the source; spans may override it.</summary>
    public TextCase Case { get; init; }
    /// <summary>Whether font pair kerning applies; spans may override it.</summary>
    public bool Kerning { get; init; } = true;
    /// <summary>Drawn instead of characters no font can render, or null for the font's own missing-glyph box.</summary>
    public string? MissingGlyph { get; init; }

    /// <inheritdoc />
    public bool Equals(TextLayoutOptions? other) => other is not null && (ReferenceEquals(this, other)
        || (Scalars().Equals(other.Scalars()) && TabStops.SequenceEqual(other.TabStops)));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Scalars(), TabStops.Count);

    /// <summary>Every option except the tab stop list, which compares by its elements.</summary>
    (TextWrapMode, float, int, string, float, float, float, float, float, TextAlignment?, TextAlignment, TextCase,
        bool, string?) Scalars() => (WrapMode, LineHeight, MaxLines, Ellipsis, LetterSpacing, WordSpacing, TabSize,
        FirstLineIndent, HangingIndent, Alignment, LastLineAlignment, Case, Kerning, MissingGlyph);
}

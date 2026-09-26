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

/// <summary>Display layout options shared by measurement and drawing.</summary>
public sealed record TextLayoutOptions
{
    /// <summary>Wrapping behavior when a positive width is supplied.</summary>
    public TextWrapMode WrapMode { get; init; } = TextWrapMode.Word;
    /// <summary>Line box height as a multiple of font size.</summary>
    public float LineHeight { get; init; } = 1.2f;
    /// <summary>Maximum number of visible lines; zero means unlimited.</summary>
    public int MaxLines { get; init; }
    /// <summary>Marker shown when lines are removed by <see cref="MaxLines"/>.</summary>
    public string Ellipsis { get; init; } = "…";
}

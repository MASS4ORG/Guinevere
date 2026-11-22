using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [TextArea] grows between its minimum and maximum lines, then scrolls.
/// <summary>Demonstrates multiline text editors with different line limits.</summary>
public sealed class TextAreaDemo
{
    /// <summary>Notes edited in a multiline field.</summary>
    [TextArea] public string Notes { get; set; } = "Three lines minimum,\nten at most.";

    /// <summary>Short text edited in a field capped at four lines.</summary>
    [TextArea(1, 4)] public string Short { get; set; } = "Starts at one line";
}

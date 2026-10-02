using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [TextArea] grows between its minimum and maximum lines, then scrolls.
public sealed class TextAreaDemo
{
    [TextArea] public string Notes { get; set; } = "Three lines minimum,\nten at most.";

    [TextArea(1, 4)] public string Short { get; set; } = "Starts at one line";
}

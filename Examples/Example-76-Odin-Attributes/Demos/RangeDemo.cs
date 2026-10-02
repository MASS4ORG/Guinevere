using MASS4.Attributes;

namespace Example_76_Odin_Attributes;

// [Range] clamps typing and label scrubbing (drag the label) between two bounds.
public sealed class RangeDemo
{
    [Range(0, 1)] public float Volume { get; set; } = 0.5f;

    [Range(1, 99)] public int Level { get; set; } = 1;
}

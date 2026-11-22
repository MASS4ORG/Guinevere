using MASS4.Attributes;

namespace Example11ExcaliburAttributes;

// [Range] clamps typing and label scrubbing (drag the label) between two bounds.
/// <summary>Demonstrates bounded numeric property editors.</summary>
public sealed class RangeDemo
{
    /// <summary>Volume constrained to the normalized range.</summary>
    [Range(0, 1)] public float Volume { get; set; } = 0.5f;

    /// <summary>Level constrained to values from one through ninety-nine.</summary>
    [Range(1, 99)] public int Level { get; set; } = 1;
}

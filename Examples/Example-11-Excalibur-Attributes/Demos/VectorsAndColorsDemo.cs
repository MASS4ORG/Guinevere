using System.Numerics;
using Guinevere;

namespace Example11ExcaliburAttributes;

// Vectors edit per axis (drag an axis letter to scrub); colors edit per channel.
/// <summary>Demonstrates vector and color property editors.</summary>
public sealed class VectorsAndColorsDemo
{
    /// <summary>Two-dimensional offset shown in the editor.</summary>
    public Vector2 Offset { get; set; } = new(10, 20);
    /// <summary>Three-dimensional position shown in the editor.</summary>
    public Vector3 Position { get; set; } = new(0, 1.5f, -3);
    /// <summary>Four-sided margins shown in the editor.</summary>
    public Vector4 Margins { get; set; } = new(4, 8, 4, 8);
    /// <summary>Color tint shown with editable channels.</summary>
    public Color Tint { get; set; } = Color.FromArgb(255, 84, 143, 224);
}

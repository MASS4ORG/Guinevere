using System.Numerics;
using Guinevere;

namespace Example_76_Odin_Attributes;

// Vectors edit per axis (drag an axis letter to scrub); colors edit per channel.
public sealed class VectorsAndColorsDemo
{
    public Vector2 Offset { get; set; } = new(10, 20);
    public Vector3 Position { get; set; } = new(0, 1.5f, -3);
    public Vector4 Margins { get; set; } = new(4, 8, 4, 8);
    public Color Tint { get; set; } = Color.FromArgb(255, 84, 143, 224);
}

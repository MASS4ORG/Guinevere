namespace Example_76_Odin_Attributes;

public struct Bounds
{
    public float Min { get; set; }
    public float Max { get; set; }
}

// A struct is a copy, so every edit is written back to the property that holds it.
public sealed class NestedStructsDemo
{
    public Bounds SpawnHeight { get; set; } = new() { Min = 0, Max = 10 };
    public List<Bounds> Zones { get; set; } = [new() { Min = -5, Max = 5 }];
}

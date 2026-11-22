namespace Example11ExcaliburAttributes;

/// <summary>Minimum and maximum values for a numeric interval.</summary>
public struct Bounds
{
    /// <summary>Lower end of the interval.</summary>
    public float Min { get; set; }
    /// <summary>Upper end of the interval.</summary>
    public float Max { get; set; }
}

/// <summary>Demonstrates writing edits back to nested value types.</summary>
public sealed class NestedStructsDemo
{
    /// <summary>Allowed vertical range for spawning.</summary>
    public Bounds SpawnHeight { get; set; } = new() { Min = 0, Max = 10 };
    /// <summary>Configured numeric zones.</summary>
    public List<Bounds> Zones { get; set; } = [new() { Min = -5, Max = 5 }];
}

namespace Example11ExcaliburAttributes;

// Nullable values edit like their underlying type.
/// <summary>Demonstrates optional numeric and enum values.</summary>
public sealed class NullablesDemo
{
    /// <summary>Optional time limit in seconds.</summary>
    public int? TimeLimit { get; set; } = 120;
    /// <summary>Optional movement handicap.</summary>
    public float? Handicap { get; set; }
    /// <summary>Optional difficulty override.</summary>
    public Difficulty? Override { get; set; }
}

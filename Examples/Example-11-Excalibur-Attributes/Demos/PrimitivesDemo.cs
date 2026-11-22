namespace Example11ExcaliburAttributes;

/// <summary>Difficulty levels used by the primitive field editor example.</summary>
public enum Difficulty
{
    /// <summary>Uses the lowest challenge level.</summary>
    Easy,
    /// <summary>Uses the default challenge level.</summary>
    Normal,
    /// <summary>Uses the highest challenge level.</summary>
    Hard
}

/// <summary>Demonstrates editors for public primitive properties without attributes.</summary>
public sealed class PrimitivesDemo
{
    /// <summary>Whether the example feature is enabled.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>Name displayed for the example player.</summary>
    public string PlayerName { get; set; } = "Arthur";
    /// <summary>Selected challenge level.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    /// <summary>Number of remaining lives.</summary>
    public int Lives { get; set; } = 3;
    /// <summary>Movement speed used by the example.</summary>
    public float Speed { get; set; } = 4.5f;
    /// <summary>Gravity value used by the example.</summary>
    public double Gravity { get; set; } = 9.81;
    /// <summary>Opacity value represented as a byte.</summary>
    public byte Opacity { get; set; } = 255;
    /// <summary>Accumulated example score.</summary>
    public long Score { get; set; } = 1_000_000;
}

namespace Example_76_Odin_Attributes;

// Nullable values edit like their underlying type.
public sealed class NullablesDemo
{
    public int? TimeLimit { get; set; } = 120;
    public float? Handicap { get; set; }
    public Difficulty? Override { get; set; }
}
